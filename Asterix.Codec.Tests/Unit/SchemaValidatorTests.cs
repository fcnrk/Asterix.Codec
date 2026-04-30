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

    // ── optional_group validation ─────────────────────────────────────────────

    [Fact]
    public void Validate_OptionalGroup_MissingPresenceGroup_Throws()
    {
        var def = new SpfFieldSetDefinition("TEST", "",
        [
            new ScalarEntry("length", FieldType.UInt, 16),
            // No DynamicPresenceEntry named "presence"
            new OptionalGroupEntry("grp1", "presence", "grp1",
            [
                new FieldDefinition("ga", FieldType.UInt, 8, 0),
            ]),
        ]);
        var schema = new SpfFieldSetSchema(1, new Dictionary<string, SpfFieldSetDefinition>
            { ["TEST"] = def });

        Action act = () => SchemaValidator.Validate(schema, "test");

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*presence group 'presence'*");
    }

    [Fact]
    public void Validate_OptionalGroup_InvalidPresenceField_Throws()
    {
        var def = new SpfFieldSetDefinition("TEST", "",
        [
            new ScalarEntry("length", FieldType.UInt, 16),
            new DynamicPresenceEntry("presence", 8, ["other"]),
            new OptionalGroupEntry("grp1", "presence", "grp1",   // "grp1" not in presence.fields
            [
                new FieldDefinition("ga", FieldType.UInt, 8, 0),
            ]),
        ]);
        var schema = new SpfFieldSetSchema(1, new Dictionary<string, SpfFieldSetDefinition>
            { ["TEST"] = def });

        Action act = () => SchemaValidator.Validate(schema, "test");

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*presence field 'grp1'*");
    }

    [Fact]
    public void Validate_OptionalGroup_EmptyFields_Throws()
    {
        var def = new SpfFieldSetDefinition("TEST", "",
        [
            new ScalarEntry("length", FieldType.UInt, 16),
            new DynamicPresenceEntry("presence", 8, ["grp1"]),
            new OptionalGroupEntry("grp1", "presence", "grp1", []),
        ]);
        var schema = new SpfFieldSetSchema(1, new Dictionary<string, SpfFieldSetDefinition>
            { ["TEST"] = def });

        Action act = () => SchemaValidator.Validate(schema, "test");

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*empty*fields*");
    }

    // ── optional_repetitive validation ────────────────────────────────────────

    [Fact]
    public void Validate_OptionalRepetitive_MissingPresenceGroup_Throws()
    {
        var def = new SpfFieldSetDefinition("TEST", "",
        [
            new ScalarEntry("length", FieldType.UInt, 16),
            new OptionalRepetitiveEntry("rep1", "presence", "rep1",
                new SpfElementDefinition([new FieldDefinition("ra", FieldType.UInt, 8, 0)])),
        ]);
        var schema = new SpfFieldSetSchema(1, new Dictionary<string, SpfFieldSetDefinition>
            { ["TEST"] = def });

        Action act = () => SchemaValidator.Validate(schema, "test");

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*presence group 'presence'*");
    }

    [Fact]
    public void Validate_OptionalRepetitive_InvalidPresenceField_Throws()
    {
        var def = new SpfFieldSetDefinition("TEST", "",
        [
            new ScalarEntry("length", FieldType.UInt, 16),
            new DynamicPresenceEntry("presence", 8, ["other"]),
            new OptionalRepetitiveEntry("rep1", "presence", "rep1",
                new SpfElementDefinition([new FieldDefinition("ra", FieldType.UInt, 8, 0)])),
        ]);
        var schema = new SpfFieldSetSchema(1, new Dictionary<string, SpfFieldSetDefinition>
            { ["TEST"] = def });

        Action act = () => SchemaValidator.Validate(schema, "test");

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*presence field 'rep1'*");
    }

    [Fact]
    public void Validate_OptionalRepetitive_EmptyElement_Throws()
    {
        var def = new SpfFieldSetDefinition("TEST", "",
        [
            new ScalarEntry("length", FieldType.UInt, 16),
            new DynamicPresenceEntry("presence", 8, ["rep1"]),
            new OptionalRepetitiveEntry("rep1", "presence", "rep1",
                new SpfElementDefinition([])),
        ]);
        var schema = new SpfFieldSetSchema(1, new Dictionary<string, SpfFieldSetDefinition>
            { ["TEST"] = def });

        Action act = () => SchemaValidator.Validate(schema, "test");

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*empty element*");
    }

    [Fact]
    public void Validate_ValidSpfExtended_DoesNotThrow()
    {
        var def = SchemaFixtures.SpfExtended();
        var schema = new SpfFieldSetSchema(1, new Dictionary<string, SpfFieldSetDefinition>
            { [def.Name] = def });

        Action act = () => SchemaValidator.Validate(schema, "test");

        act.Should().NotThrow();
    }
}
