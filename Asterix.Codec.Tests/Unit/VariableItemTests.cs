using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using Asterix.Codec.Decode.ItemDecoders;
using Asterix.Codec.Encode;
using Asterix.Codec.Encode.ItemEncoders;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

/// <summary>
/// Tests for variable-length item decode, encode, and round-trip.
///
/// Test schema (2-group variable item):
///
///   Group 0 (primary):   cnf(1) rad(2) dou(1) mah(1) cdm(2) — 7 bits + FX
///   Group 1 (extension): tre(1) gho(1) sup(1) tcc(1) spare(3) — 7 bits + FX
///
/// Wire values used in tests:
///   Byte 0: 0xD3 = 1101 0011  → cnf=1, rad=2, dou=1, mah=0, cdm=1, FX=1
///   Byte 1: 0x50 = 0101 0000  → tre=0, gho=1, sup=0, tcc=1, spare=0, FX=0
/// </summary>
public class VariableItemTests
{
    // ── Schema helpers ────────────────────────────────────────────────────────

    private static VariableItemDefinition TwoGroupDefinition() => new(
    [
        new VariableGroupDefinition([         // primary: 7 bits = cnf(1)+rad(2)+dou(1)+mah(1)+cdm(2)
            new("cnf", FieldType.Bool, 1, bitOffset: 0),
            new("rad", FieldType.UInt, 2, bitOffset: 1),
            new("dou", FieldType.Bool, 1, bitOffset: 3),
            new("mah", FieldType.Bool, 1, bitOffset: 4),
            new("cdm", FieldType.UInt, 2, bitOffset: 5),
        ]),
        new VariableGroupDefinition([         // extension: 7 bits = tre(1)+gho(1)+sup(1)+tcc(1)+spare(3)
            new("tre", FieldType.Bool, 1, bitOffset: 0),
            new("gho", FieldType.Bool, 1, bitOffset: 1),
            new("sup", FieldType.Bool, 1, bitOffset: 2),
            new("tcc", FieldType.Bool, 1, bitOffset: 3),
            // bits 4..6 are spare — no field definition
        ]),
    ]);

    private static VariableItemDefinition OneGroupDefinition() => new(
    [
        new VariableGroupDefinition([
            new("cnf", FieldType.Bool, 1, bitOffset: 0),
            new("rad", FieldType.UInt, 2, bitOffset: 1),
            new("dou", FieldType.Bool, 1, bitOffset: 3),
            new("mah", FieldType.Bool, 1, bitOffset: 4),
            new("cdm", FieldType.UInt, 2, bitOffset: 5),
        ]),
    ]);

    // ── Decode: single group (no extension) ───────────────────────────────────

    [Fact]
    public void Decode_SingleGroup_NoExtension_OneByte()
    {
        // 0xA2 = 1010 0010: cnf=1, rad=0, dou=1, mah=0, cdm=1, FX=0
        // cnf=bit7=1, rad=bits6-5=01=1... wait let me recalculate
        // 0xA2 = 1010 0010
        // bit7=1=cnf, bit6-5=01=1=rad, bit4=0=dou, bit3=0=mah, bit2-1=01=1=cdm, bit0=0=FX
        byte[] data = [0xA2];
        var reader = new BitReader(data);
        var def = OneGroupDefinition();

        VariableDecodedItem item = VariableItemDecoder.Decode(ref reader, def, "TEST", DecodeMode.Strict);

        item.Groups.Should().HaveCount(1);
        item.GetField("cnf")!.RawValue.Should().Be(1UL);
        item.GetField("rad")!.RawValue.Should().Be(1UL);
        item.GetField("dou")!.RawValue.Should().Be(0UL);
        item.GetField("mah")!.RawValue.Should().Be(0UL);
        item.GetField("cdm")!.RawValue.Should().Be(1UL);
    }

    [Fact]
    public void Decode_SingleGroup_FxZero_ConsumesExactlyOneByte()
    {
        // FX=0 in the only byte → stop after 1 byte
        byte[] data = [0x00, 0xFF]; // second byte is sentinel — should not be read
        var reader = new BitReader(data);
        var def = OneGroupDefinition();

        VariableItemDecoder.Decode(ref reader, def, "TEST", DecodeMode.Strict);

        reader.ByteOffset.Should().Be(1, "only the first byte should be consumed");
    }

    // ── Decode: two groups (FX=1 in first, FX=0 in second) ───────────────────

    [Fact]
    public void Decode_TwoGroups_PrimaryAndExtension()
    {
        // Byte 0: 0xD3 = 1101 0011 → cnf=1, rad=2(10), dou=1, mah=0, cdm=1(01), FX=1
        // Byte 1: 0x50 = 0101 0000 → tre=0, gho=1, sup=0, tcc=1, spare=0, FX=0
        byte[] data = [0xD3, 0x50];
        var reader = new BitReader(data);
        var def = TwoGroupDefinition();

        VariableDecodedItem item = VariableItemDecoder.Decode(ref reader, def, "TEST", DecodeMode.Strict);

        item.Groups.Should().HaveCount(2);

        // Group 0 (primary)
        item.GetField("cnf")!.RawValue.Should().Be(1UL);  // bit7=1
        item.GetField("rad")!.RawValue.Should().Be(2UL);  // bits6-5=10
        item.GetField("dou")!.RawValue.Should().Be(1UL);  // bit4=1
        item.GetField("mah")!.RawValue.Should().Be(0UL);  // bit3=0
        item.GetField("cdm")!.RawValue.Should().Be(1UL);  // bits2-1=01

        // Group 1 (extension)
        item.GetField("tre")!.RawValue.Should().Be(0UL);  // bit7=0
        item.GetField("gho")!.RawValue.Should().Be(1UL);  // bit6=1
        item.GetField("sup")!.RawValue.Should().Be(0UL);  // bit5=0
        item.GetField("tcc")!.RawValue.Should().Be(1UL);  // bit4=1
    }

    [Fact]
    public void Decode_TwoGroups_ConsumesBothBytes()
    {
        byte[] data = [0xD3, 0x50, 0xFF]; // third byte is sentinel
        var reader = new BitReader(data);

        VariableItemDecoder.Decode(ref reader, TwoGroupDefinition(), "TEST", DecodeMode.Strict);

        reader.ByteOffset.Should().Be(2);
    }

    // ── Strict mode: extra groups ─────────────────────────────────────────────

    [Fact]
    public void Decode_MoreGroupsThanDefined_StrictMode_Throws()
    {
        // FX=1 in byte 0 → would need a second group, but schema only has 1
        byte[] data = [0x01, 0x00]; // 0x01 = 0000 0001 → all fields=0, FX=1 → needs extension
        var reader = new BitReader(data);
        var def = OneGroupDefinition(); // only 1 group defined

        Assert.Throws<Exceptions.DecodeException>(() =>
        {
            var r = new BitReader(data);
            VariableItemDecoder.Decode(ref r, def, "TEST", DecodeMode.Strict);
        });
    }

    [Fact]
    public void Decode_MoreGroupsThanDefined_LenientMode_DiscardExtra()
    {
        byte[] data = [0x01, 0x00]; // FX=1 in byte 0, byte 1 is extra
        var def = OneGroupDefinition();

        var reader = new BitReader(data);
        var item = VariableItemDecoder.Decode(ref reader, def, "TEST", DecodeMode.Lenient);

        // Primary group decoded, extra group discarded but consumed
        item.Groups.Should().HaveCount(1);
        reader.RemainingBits.Should().Be(0, "both bytes consumed even though second was extra");
    }

    // ── GetField across groups ─────────────────────────────────────────────────

    [Fact]
    public void GetField_MissingName_ReturnsNull()
    {
        byte[] data = [0x00]; // single group, FX=0
        var reader = new BitReader(data);
        var item = VariableItemDecoder.Decode(ref reader, OneGroupDefinition(), "TEST", DecodeMode.Strict);

        item.GetField("nonexistent").Should().BeNull();
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_TwoGroups_ByteForByte()
    {
        byte[] original = [0xD3, 0x50];
        var def = TwoGroupDefinition();

        var reader = new BitReader(original);
        VariableDecodedItem decoded = VariableItemDecoder.Decode(ref reader, def, "TEST", DecodeMode.Strict);

        var writer = new BitWriter();
        VariableItemEncoder.Encode(writer, decoded, def, "TEST");

        writer.ToArray().Should().Equal(original);
    }

    [Fact]
    public void RoundTrip_SingleGroup_ByteForByte()
    {
        byte[] original = [0xA2]; // FX=0 → 1010 0010
        var def = OneGroupDefinition();

        var reader = new BitReader(original);
        VariableDecodedItem decoded = VariableItemDecoder.Decode(ref reader, def, "TEST", DecodeMode.Strict);

        var writer = new BitWriter();
        VariableItemEncoder.Encode(writer, decoded, def, "TEST");

        writer.ToArray().Should().Equal(original);
    }

    [Fact]
    public void Encode_FxBitsRebuiltFromGroupCount()
    {
        // If 2 groups are present, byte 0's FX must be 1; byte 1's FX must be 0.
        byte[] original = [0xD3, 0x50];
        var def = TwoGroupDefinition();

        var reader = new BitReader(original);
        VariableDecodedItem decoded = VariableItemDecoder.Decode(ref reader, def, "TEST", DecodeMode.Strict);

        var writer = new BitWriter();
        VariableItemEncoder.Encode(writer, decoded, def, "TEST");

        byte[] encoded = writer.ToArray();
        (encoded[0] & 0x01).Should().Be(1, "FX=1 in first byte when two groups present");
        (encoded[1] & 0x01).Should().Be(0, "FX=0 in last byte");
    }
}
