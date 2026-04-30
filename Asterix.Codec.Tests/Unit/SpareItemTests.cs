using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class SpareItemTests
{
    [Fact]
    public void LoadCategory_SpareType_ReturnsSpareItemDefinition()
    {
        const string yaml = """
            schema_version: 1
            category: 99
            name: Test
            messages:
              - id: default
                name: Test
                discriminator: null
                uap:
                  - ITEM_A
                  - SPARE
            items:
              ITEM_A:
                type: fixed
                length: 1
                fields:
                  - name: x
                    type: uint
                    bits: 8
              SPARE:
                type: spare
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        var schema = YamlSchemaLoader.LoadCategory(stream, "test");

        schema.Items.Should().ContainKey("SPARE");
        schema.Items["SPARE"].Should().BeOfType<SpareItemDefinition>();
    }

    [Fact]
    public void SchemaValidator_SpareItemInUap_DoesNotThrow()
    {
        var schema = new AsterixCategorySchema(99, "Test", 1,
            messages: [new MessageDefinition("default", "Test", null, ["ITEM_A", "SPARE"])],
            items: new Dictionary<string, ItemDefinition>
            {
                ["ITEM_A"] = new FixedItemDefinition(1, [new("x", FieldType.UInt, 8, 0)]),
                ["SPARE"]  = new SpareItemDefinition(),
            });

        var act = () => SchemaValidator.Validate(schema, "test");
        act.Should().NotThrow();
    }
}
