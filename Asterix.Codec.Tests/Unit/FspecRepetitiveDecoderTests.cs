using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using Asterix.Codec.Decode.ItemDecoders;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class FspecRepetitiveDecoderTests
{
    // Element definition used throughout: 4-byte fixed (SAC 8-bit, SIC 8-bit, STN 16-bit)
    private static FspecRepetitiveItemDefinition MakeDefinition() =>
        new(new FixedItemDefinition(4,
        [
            new FieldDefinition("sac",          FieldType.UInt, 8,  bitOffset: 0),
            new FieldDefinition("sic",          FieldType.UInt, 8,  bitOffset: 8),
            new FieldDefinition("track_number", FieldType.UInt, 16, bitOffset: 16),
        ]));

    private static FspecRepetitiveDecodedItem Decode(byte[] bytes)
    {
        var reader = new BitReader(bytes);
        return FspecRepetitiveItemDecoder.Decode(
            ref reader, MakeDefinition(), "I062_510", DecodeMode.Strict);
    }

    // ── Zero elements ─────────────────────────────────────────────────────────

    [Fact]
    public void Decode_ZeroElements_ReturnsEmptyList()
    {
        // FSPEC byte: 0x00 (no bits set, FX=0) → 0 elements
        var item = Decode([0x00]);
        item.Count.Should().Be(0);
        item.Elements.Should().BeEmpty();
    }

    // ── One element ───────────────────────────────────────────────────────────

    [Fact]
    public void Decode_OneElement_ReturnsSingleElement()
    {
        // FSPEC: 0x80 (bit 7 = 1 element, FX=0)
        // element: SAC=1, SIC=2, STN=0x0100
        var item = Decode([0x80, 0x01, 0x02, 0x01, 0x00]);

        item.Count.Should().Be(1);
        var el = item.Elements[0].Should().BeOfType<FixedDecodedItem>().Subject;
        el.GetField("sac")!.RawValue.Should().Be(1UL);
        el.GetField("sic")!.RawValue.Should().Be(2UL);
        el.GetField("track_number")!.RawValue.Should().Be(0x0100UL);
    }

    // ── Two elements ──────────────────────────────────────────────────────────

    [Fact]
    public void Decode_TwoElements_ReturnsCorrectFields()
    {
        // FSPEC: 0xC0 (bits 7+6 set, FX=0) → 2 elements
        var item = Decode(
        [
            0xC0,
            0x01, 0x02, 0x01, 0x00,   // SAC=1, SIC=2, STN=256
            0x03, 0x04, 0x02, 0x00,   // SAC=3, SIC=4, STN=512
        ]);

        item.Count.Should().Be(2);

        var el0 = item.Elements[0].Should().BeOfType<FixedDecodedItem>().Subject;
        el0.GetField("sac")!.RawValue.Should().Be(1UL);
        el0.GetField("sic")!.RawValue.Should().Be(2UL);
        el0.GetField("track_number")!.RawValue.Should().Be(256UL);

        var el1 = item.Elements[1].Should().BeOfType<FixedDecodedItem>().Subject;
        el1.GetField("sac")!.RawValue.Should().Be(3UL);
        el1.GetField("sic")!.RawValue.Should().Be(4UL);
        el1.GetField("track_number")!.RawValue.Should().Be(512UL);
    }

    // ── Seven elements — fills one FSPEC byte exactly ─────────────────────────

    [Fact]
    public void Decode_SevenElements_SingleFspecByte()
    {
        // FSPEC: 0xFE (bits 7..1 all set, FX=0) → 7 elements
        // 7 × 4-byte elements = 28 bytes + 1 FSPEC byte = 29 bytes
        var bytes = new List<byte> { 0xFE };
        for (int i = 0; i < 7; i++)
            bytes.AddRange([(byte)(i + 1), 0x00, 0x00, 0x00]);

        var item = Decode(bytes.ToArray());
        item.Count.Should().Be(7);
        for (int i = 0; i < 7; i++)
        {
            var el = item.Elements[i].Should().BeOfType<FixedDecodedItem>().Subject;
            el.GetField("sac")!.RawValue.Should().Be((ulong)(i + 1));
        }
    }

    // ── Eight elements — spills into second FSPEC byte ────────────────────────

    [Fact]
    public void Decode_EightElements_TwoFspecBytes()
    {
        // FSPEC byte 0: 0xFF (bits 7..1 set, FX=1) → 7 elements + more follow
        // FSPEC byte 1: 0x80 (bit 7 set, FX=0)     → 1 more element
        // Total: 8 elements
        var bytes = new List<byte> { 0xFF, 0x80 };
        for (int i = 0; i < 8; i++)
            bytes.AddRange([(byte)(i + 1), 0x00, 0x00, 0x00]);

        var item = Decode(bytes.ToArray());
        item.Count.Should().Be(8);
        for (int i = 0; i < 8; i++)
        {
            var el = item.Elements[i].Should().BeOfType<FixedDecodedItem>().Subject;
            el.GetField("sac")!.RawValue.Should().Be((ulong)(i + 1));
        }
    }

    // ── Strict mode: truncated data ───────────────────────────────────────────

    [Fact]
    public void Decode_Strict_TruncatedElement_ThrowsDecodeException()
    {
        // FSPEC says 1 element (4 bytes) but only 2 bytes of element data follow
        var act = () => Decode([0x80, 0x01, 0x02]);
        act.Should().Throw<DecodeException>();
    }
}
