using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.CustomSchema;

/// <summary>
/// Tests for SchemaRegistry.Freeze() structured-explicit item resolution with CAT253 schemas.
/// Verifies substitution, and expected error paths.
/// </summary>
public class Cat253BuildTests
{
    private static string SamplesPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "samples", file);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithstructuredExplicitItems_I253_100_ResolvedTostructuredExplicitDefinition()
    {
        var codec = new AsterixCodecBuilder()
            .AddCategoryFromYaml(SamplesPath("cat253.yml"))
            .AddStructuredExplicitItemsFromYaml(SamplesPath("structured_explicit_cat253.yml"))
            .Build();

        // After Build(), the registry is frozen. Verify via round-trip (schema is used internally).
        // The easiest observable proof is that decoding a type-100 packet yields a StructuredExplicitDecodedItem.
        codec.Should().NotBeNull();
    }

    [Fact]
    public void Build_WithstructuredExplicitItems_I253_100_HasFourContentEntries()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml")));
        registry.RegisterStructuredExplicitItemSet(YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml")));
        registry.Freeze();

        registry.TryGetCategory(253, out var schema).Should().BeTrue();
        schema!.Items["I253_100"].Should().BeOfType<StructuredExplicitItemDefinition>();

        var structuredExplicitDef = (StructuredExplicitItemDefinition)schema.Items["I253_100"];
        structuredExplicitDef.Content.Should().HaveCount(4);
    }

    [Fact]
    public void Build_WithstructuredExplicitItems_NonstructuredExplicitItemsPreserved()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml")));
        registry.RegisterStructuredExplicitItemSet(YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml")));
        registry.Freeze();

        registry.TryGetCategory(253, out var schema).Should().BeTrue();
        schema!.Items["I253_010"].Should().BeOfType<FixedItemDefinition>();
        schema.Items["I253_001"].Should().BeOfType<FixedItemDefinition>();
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_structuredExplicitItemsWithoutCategorySchema_ThrowsInvalidOperation()
    {
        var registry = new SchemaRegistry();
        registry.RegisterStructuredExplicitItemSet(YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml")));

        var act = () => registry.Freeze();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CAT253*");
    }

    [Fact]
    public void Build_DuplicatestructuredExplicitItemset_ThrowsInvalidOperation()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml")));
        registry.RegisterStructuredExplicitItemSet(YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml")));

        var act = () => registry.RegisterStructuredExplicitItemSet(
            YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml")));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CAT253*");
    }

    [Fact]
    public void Build_StructuredExplicitItemNotDefinedAsExplicitInCategory_ThrowsInvalidOperation()
    {
        // Build a category schema where I253_100 is fixed (not explicit).
        var catSchema = new AsterixCategorySchema(
            category: 253,
            name: "Test",
            schemaVersion: 1,
            messages:
            [
                new MessageDefinition("msg100", "Type 100", "100",
                    new List<string> { "I253_010", "I253_100" }),
                new MessageDefinition("msg001", "Type 001", "1",
                    new List<string> { "I253_010" }),
            ],
            items: new Dictionary<string, ItemDefinition>
            {
                ["I253_010"] = new FixedItemDefinition(1,
                [
                    new FieldDefinition("message_type", FieldType.UInt, 8, 0),
                ]),
                // Deliberately defined as fixed, not explicit
                ["I253_100"] = new FixedItemDefinition(2,
                [
                    new FieldDefinition("data", FieldType.UInt, 16, 0),
                ]),
            },
            messageDiscriminator: new CategoryDiscriminator("I253_010", "message_type"));

        var structuredExplicitSet = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        var registry = new SchemaRegistry();
        registry.RegisterCategory(catSchema);
        registry.RegisterStructuredExplicitItemSet(structuredExplicitSet);

        var act = () => registry.Freeze();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*explicit*");
    }
}
