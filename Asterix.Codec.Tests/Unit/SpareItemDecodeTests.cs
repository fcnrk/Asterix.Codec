using Asterix.Codec.Decode;
using Asterix.Codec.Model;
using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class SpareItemDecodeTests
{
    [Fact]
    public void Decode_SpareUapPositionBitSet_SpareIsSkippedNotInOutput()
    {
        // UAP: ITEM_A(FRN1), SPARE(FRN2), ITEM_B(FRN3)
        // FSPEC: 0xE0 = bits 7+6+5 set → ITEM_A + SPARE + ITEM_B present
        // Spare bit (FRN2) is set (malformed packet) — decoder must skip it
        // ITEM_A = 1 byte (0x01), ITEM_B = 1 byte (0x02)
        var schema = new AsterixCategorySchema(99, "Test", 1,
            messages: [new MessageDefinition("default", "Test", null, ["ITEM_A", "SPARE", "ITEM_B"])],
            items: new Dictionary<string, ItemDefinition>
            {
                ["ITEM_A"] = new FixedItemDefinition(1, [new("x", FieldType.UInt, 8, 0)]),
                ["SPARE"]  = new SpareItemDefinition(),
                ["ITEM_B"] = new FixedItemDefinition(1, [new("y", FieldType.UInt, 8, 0)]),
            });

        var registry = new SchemaRegistry();
        registry.RegisterCategory(schema);
        registry.Freeze();
        var decoder = new AsterixDecoder(registry, DecodeMode.Lenient);

        byte[] packet =
        [
            0x63,       // CAT = 99
            0x00, 0x06, // LEN = 6 (1 CAT + 2 LEN + 1 FSPEC + 1 ITEM_A + 1 ITEM_B)
            0xE0,       // FSPEC: ITEM_A(bit7) + SPARE(bit6) + ITEM_B(bit5), no FX
            0x01,       // ITEM_A.x = 1
            0x02,       // ITEM_B.y = 2
        ];

        var result = decoder.Decode(packet);
        var record = result.Records[0];

        record.Items.Should().ContainKey("ITEM_A");
        record.Items.Should().NotContainKey("SPARE");
        record.Items.Should().ContainKey("ITEM_B");

        var itemB = record.Items["ITEM_B"].Should().BeOfType<FixedDecodedItem>().Subject;
        itemB.GetField("y")!.RawValue.Should().Be(2UL);
    }
}
