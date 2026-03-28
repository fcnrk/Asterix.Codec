using Asterix.Codec.Exceptions;
using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

/// <summary>
/// Tests for SchemaValidator — verifies that invalid cross-references and structural
/// errors are caught before any schema is used at runtime.
/// </summary>
public class SchemaValidatorTests
{
    // ── Category validation ───────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidCat062_DoesNotThrow()
    {
        var schema = SchemaFixtures.Cat062Schema();
        var act = () => SchemaValidator.Validate(schema, "test");
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_UapReferencesNonExistentItem_Throws()
    {
        var schema = new AsterixCategorySchema(62, "Test", 1,
            messages: [new MessageDefinition("default", "test", null, ["MISSING_ITEM"])],
            items: new Dictionary<string, ItemDefinition>());

        Assert.Throws<SchemaValidationException>(() =>
            SchemaValidator.Validate(schema, "test"));
    }

    [Fact]
    public void Validate_CompoundFspecReferencesNonExistentSubitem_Throws()
    {
        var compound = new CompoundItemDefinition(
            fspec: ["existing", "DOES_NOT_EXIST"],
            subitems: new Dictionary<string, ItemDefinition>
            {
                ["existing"] = new FixedItemDefinition(1, [new("f", FieldType.UInt, 8, 0)])
            });

        var schema = new AsterixCategorySchema(62, "Test", 1,
            messages: [new MessageDefinition("default", "test", null, ["I062_010"])],
            items: new Dictionary<string, ItemDefinition>
            {
                ["I062_010"] = compound
            });

        Assert.Throws<SchemaValidationException>(() =>
            SchemaValidator.Validate(schema, "test"));
    }

    [Fact]
    public void Validate_ValidCompound_DoesNotThrow()
    {
        var compound = new CompoundItemDefinition(
            fspec: ["a"],
            subitems: new Dictionary<string, ItemDefinition>
            {
                ["a"] = new FixedItemDefinition(1, [new("f", FieldType.UInt, 8, 0)])
            });

        var schema = new AsterixCategorySchema(62, "Test", 1,
            messages: [new MessageDefinition("default", "test", null, ["X"])],
            items: new Dictionary<string, ItemDefinition> { ["X"] = compound });

        var act = () => SchemaValidator.Validate(schema, "test");
        act.Should().NotThrow();
    }

    // ── SPF validation ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidSpf_DoesNotThrow()
    {
        var spfDef = SchemaFixtures.SpfCustom062();
        var schema = new SpfFieldSetSchema(1,
            new Dictionary<string, SpfFieldSetDefinition> { [spfDef.Name] = spfDef });

        var act = () => SchemaValidator.Validate(schema, "test");
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_SpfCountRefForwardReference_Throws()
    {
        // count_ref points to a scalar that comes AFTER the repetitive entry — invalid
        var structure = new List<SpfStructureEntry>
        {
            new SpfRepetitiveEntry("rep", "count", new SpfElementDefinition([])),  // count not yet defined
            new ScalarEntry("count", FieldType.UInt, 8),
        };
        var spfDef = new SpfFieldSetDefinition("BAD_SPF", "test", structure);
        var schema = new SpfFieldSetSchema(1,
            new Dictionary<string, SpfFieldSetDefinition> { [spfDef.Name] = spfDef });

        Assert.Throws<SchemaValidationException>(() =>
            SchemaValidator.Validate(schema, "test"));
    }

    [Fact]
    public void Validate_SpfCountRefNonExistentName_Throws()
    {
        var structure = new List<SpfStructureEntry>
        {
            new ScalarEntry("count", FieldType.UInt, 8),
            new SpfRepetitiveEntry("rep", "MISSING_COUNT", new SpfElementDefinition([])),
        };
        var spfDef = new SpfFieldSetDefinition("BAD_SPF", "test", structure);
        var schema = new SpfFieldSetSchema(1,
            new Dictionary<string, SpfFieldSetDefinition> { [spfDef.Name] = spfDef });

        Assert.Throws<SchemaValidationException>(() =>
            SchemaValidator.Validate(schema, "test"));
    }

    [Fact]
    public void Validate_SpfPresentIfUnknownGroup_Throws()
    {
        var structure = new List<SpfStructureEntry>
        {
            new OptionalEntry("f1", "MISSING_GROUP", "f1",
                new FieldDefinition("f1", FieldType.UInt, 8, 0)),
        };
        var spfDef = new SpfFieldSetDefinition("BAD_SPF", "test", structure);
        var schema = new SpfFieldSetSchema(1,
            new Dictionary<string, SpfFieldSetDefinition> { [spfDef.Name] = spfDef });

        Assert.Throws<SchemaValidationException>(() =>
            SchemaValidator.Validate(schema, "test"));
    }

    [Fact]
    public void Validate_SpfPresentIfUnknownField_Throws()
    {
        var structure = new List<SpfStructureEntry>
        {
            new DynamicPresenceEntry("presence", 8, ["f2"]),
            new OptionalEntry("f1", "presence", "MISSING_FIELD",
                new FieldDefinition("f1", FieldType.UInt, 8, 0)),
        };
        var spfDef = new SpfFieldSetDefinition("BAD_SPF", "test", structure);
        var schema = new SpfFieldSetSchema(1,
            new Dictionary<string, SpfFieldSetDefinition> { [spfDef.Name] = spfDef });

        Assert.Throws<SchemaValidationException>(() =>
            SchemaValidator.Validate(schema, "test"));
    }
}
