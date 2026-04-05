using Asterix.Codec.Binary;
using Asterix.Codec.Encode.ItemEncoders;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class FspecRepetitiveEncoderTests
{
    private static FspecRepetitiveItemDefinition MakeDefinition() =>
        new(new FixedItemDefinition(4,
        [
            new FieldDefinition("sac",          FieldType.UInt, 8,  bitOffset: 0),
            new FieldDefinition("sic",          FieldType.UInt, 8,  bitOffset: 8),
            new FieldDefinition("track_number", FieldType.UInt, 16, bitOffset: 16),
        ]));

    private static FixedDecodedItem MakeElement(byte sac, byte sic, ushort stn) =>
        new([
            new DecodedField("sac",          sac,  null, null),
            new DecodedField("sic",          sic,  null, null),
            new DecodedField("track_number", stn,  null, null),
        ]);

    private static byte[] Encode(FspecRepetitiveDecodedItem item)
    {
        var writer = new BitWriter();
        FspecRepetitiveItemEncoder.Encode(writer, item, MakeDefinition(), "I062_510");
        return writer.ToArray();
    }

    // ── Zero elements ─────────────────────────────────────────────────────────

    [Fact]
    public void Encode_ZeroElements_WritesSingleZeroByte()
    {
        var bytes = Encode(new FspecRepetitiveDecodedItem([]));
        // FSPEC: 0x00 (no bits set, FX=0)
        bytes.Should().Equal(0x00);
    }

    // ── One element ───────────────────────────────────────────────────────────

    [Fact]
    public void Encode_OneElement_CorrectFspecAndElement()
    {
        var bytes = Encode(new FspecRepetitiveDecodedItem([MakeElement(1, 2, 0x0100)]));
        // FSPEC: 0x80 (bit 7 set, FX=0), then SAC=1, SIC=2, STN=0x0100
        bytes.Should().Equal(0x80, 0x01, 0x02, 0x01, 0x00);
    }

    // ── Two elements ──────────────────────────────────────────────────────────

    [Fact]
    public void Encode_TwoElements_CorrectBytes()
    {
        var bytes = Encode(new FspecRepetitiveDecodedItem(
        [
            MakeElement(1, 2, 0x0100),
            MakeElement(3, 4, 0x0200),
        ]));
        // FSPEC: 0xC0 (bits 7+6, FX=0)
        bytes.Should().Equal(
            0xC0,
            0x01, 0x02, 0x01, 0x00,
            0x03, 0x04, 0x02, 0x00);
    }

    // ── Seven elements — exactly one FSPEC byte ───────────────────────────────

    [Fact]
    public void Encode_SevenElements_SingleFspecByte()
    {
        var elements = Enumerable.Range(1, 7)
            .Select(i => (DecodedItem)MakeElement((byte)i, 0, 0))
            .ToList();

        var bytes = Encode(new FspecRepetitiveDecodedItem(elements));

        // FSPEC: 0xFE (bits 7..1 all set, FX=0)
        bytes[0].Should().Be(0xFE);
        bytes.Length.Should().Be(1 + 7 * 4); // 1 FSPEC + 28 element bytes
    }

    // ── Eight elements — FSPEC extends to second byte ─────────────────────────

    [Fact]
    public void Encode_EightElements_TwoFspecBytes()
    {
        var elements = Enumerable.Range(1, 8)
            .Select(i => (DecodedItem)MakeElement((byte)i, 0, 0))
            .ToList();

        var bytes = Encode(new FspecRepetitiveDecodedItem(elements));

        // FSPEC byte 0: 0xFF (bits 7..1 all set, FX=1)
        // FSPEC byte 1: 0x80 (bit 7 set, FX=0)
        bytes[0].Should().Be(0xFF);
        bytes[1].Should().Be(0x80);
        bytes.Length.Should().Be(2 + 8 * 4); // 2 FSPEC + 32 element bytes
    }
}
