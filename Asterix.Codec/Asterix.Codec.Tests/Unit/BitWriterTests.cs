using Asterix.Codec.Binary;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class BitWriterTests
{
    // ── WriteBits (unsigned) ──────────────────────────────────────────────────

    [Fact]
    public void WriteBits_SingleByte_CorrectBytes()
    {
        var writer = new BitWriter();
        writer.WriteBits(0xA5, 8);
        writer.ToArray().Should().Equal(0xA5);
    }

    [Fact]
    public void WriteBits_MsbFirst_PacksCorrectly()
    {
        // Two 4-bit nibbles → one byte
        var writer = new BitWriter();
        writer.WriteBits(0xA, 4);
        writer.WriteBits(0xB, 4);
        writer.ToArray().Should().Equal(0xAB);
    }

    [Fact]
    public void WriteBits_CrossByte_SpansCorrectly()
    {
        // Write 12 bits = 0x123 → should produce 0x12, 0x30
        var writer = new BitWriter();
        writer.WriteBits(0x123, 12);
        // 0001 0010 0011 xxxx → aligned → 0x12, 0x30 after AlignToByte
        writer.AlignToByte();
        writer.ToArray().Should().Equal(0x12, 0x30);
    }

    [Fact]
    public void WriteBits_64Bits_FullUlong()
    {
        var writer = new BitWriter();
        writer.WriteBits(0x0102030405060708UL, 64);
        writer.ToArray().Should().Equal(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08);
    }

    // ── WriteSignedBits ───────────────────────────────────────────────────────

    [Fact]
    public void WriteSignedBits_PositiveValue_CorrectBits()
    {
        var writer = new BitWriter();
        writer.WriteSignedBits(127L, 8);
        writer.ToArray().Should().Equal(0x7F);
    }

    [Fact]
    public void WriteSignedBits_NegativeOne_AllOnes()
    {
        var writer = new BitWriter();
        writer.WriteSignedBits(-1L, 8);
        writer.ToArray().Should().Equal(0xFF);
    }

    [Fact]
    public void WriteSignedBits_MinusOne_16Bit_AllOnes()
    {
        var writer = new BitWriter();
        writer.WriteSignedBits(-1L, 16);
        writer.ToArray().Should().Equal(0xFF, 0xFF);
    }

    // ── WriteBool ─────────────────────────────────────────────────────────────

    [Fact]
    public void WriteBool_True_WritesBit1()
    {
        var writer = new BitWriter();
        writer.WriteBool(true);
        writer.WriteBits(0, 7); // pad to byte
        writer.ToArray().Should().Equal(0x80);
    }

    [Fact]
    public void WriteBool_False_WritesBit0()
    {
        var writer = new BitWriter();
        writer.WriteBool(false);
        writer.WriteBits(0, 7);
        writer.ToArray().Should().Equal(0x00);
    }

    // ── WriteBytes ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteBytes_Aligned_AppendsVerbatim()
    {
        var writer = new BitWriter();
        writer.WriteBytes([0xDE, 0xAD, 0xBE, 0xEF]);
        writer.ToArray().Should().Equal(0xDE, 0xAD, 0xBE, 0xEF);
    }

    [Fact]
    public void WriteBytes_Unaligned_ThrowsInvalidOperation()
    {
        var writer = new BitWriter();
        writer.WriteBits(1, 3); // misalign
        writer.Invoking(w => w.WriteBytes([0x00]))
            .Should().Throw<InvalidOperationException>();
    }

    // ── ByteLength / BitPosition ──────────────────────────────────────────────

    [Fact]
    public void ByteLength_AfterWritingOneByte_IsOne()
    {
        var writer = new BitWriter();
        writer.WriteBits(0xFF, 8);
        writer.ByteLength.Should().Be(1);
    }

    [Fact]
    public void ByteLength_PartialByte_RoundsUp()
    {
        var writer = new BitWriter();
        writer.WriteBits(1, 3);
        writer.ByteLength.Should().Be(1);
    }

    // ── AlignToByte ───────────────────────────────────────────────────────────

    [Fact]
    public void AlignToByte_PadsWithZeroBits()
    {
        var writer = new BitWriter();
        writer.WriteBits(1, 1); // bit 7 = 1
        writer.AlignToByte();   // pads bits 6..0 with 0
        writer.ToArray().Should().Equal(0x80);
    }

    // ── Buffer growth ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteBits_ExceedsInitialCapacity_GrowsAutomatically()
    {
        var writer = new BitWriter(initialCapacity: 1);
        for (int i = 0; i < 32; i++)
            writer.WriteBits(0xFF, 8); // 32 bytes — far exceeds initial 1-byte capacity
        writer.ByteLength.Should().Be(32);
        writer.ToArray().Should().AllBeEquivalentTo((byte)0xFF);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsContent()
    {
        var writer = new BitWriter();
        writer.WriteBits(0xAB, 8);
        writer.Reset();
        writer.ByteLength.Should().Be(0);
        writer.BitPosition.Should().Be(0);
    }

    // ── BitReader round-trip ──────────────────────────────────────────────────

    [Fact]
    public void WriteRead_RoundTrip_SignedInt()
    {
        short value = -1234;

        var writer = new BitWriter();
        writer.WriteSignedBits(value, 16);

        var reader = new BitReader(writer.ToSpan());
        reader.ReadSignedBits(16).Should().Be(value);
    }

    [Fact]
    public void WriteRead_RoundTrip_SubByteUnsigned()
    {
        var writer = new BitWriter();
        writer.WriteBits(5,  3);
        writer.WriteBits(13, 5);

        var reader = new BitReader(writer.ToSpan());
        reader.ReadBits(3).Should().Be(5);
        reader.ReadBits(5).Should().Be(13);
    }
}
