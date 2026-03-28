using Asterix.Codec.Binary;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class BitReaderTests
{
    // ── ReadBits (unsigned) ───────────────────────────────────────────────────

    [Fact]
    public void ReadBits_SingleByte_AllBits_ReturnsByte()
    {
        var reader = new BitReader([0xA5]);
        reader.ReadBits(8).Should().Be(0xA5);
    }

    [Fact]
    public void ReadBits_MsbFirst_ReadsHighBitsFirst()
    {
        // 0xC0 = 1100 0000; first 2 bits should be 0b11 = 3
        var reader = new BitReader([0xC0]);
        reader.ReadBits(2).Should().Be(3);
    }

    [Fact]
    public void ReadBits_SequentialReads_AdvancesPosition()
    {
        var reader = new BitReader([0xAB]); // 1010 1011
        reader.ReadBits(4).Should().Be(0xA);  // 1010
        reader.ReadBits(4).Should().Be(0xB);  // 1011
        reader.RemainingBits.Should().Be(0);
    }

    [Fact]
    public void ReadBits_CrossByteBoundary_CombinesCorrectly()
    {
        // bytes: 0x12 0x34 = 0001 0010  0011 0100
        // read 12 bits → 0001 0010 0011 = 0x123
        var reader = new BitReader([0x12, 0x34]);
        reader.ReadBits(12).Should().Be(0x123);
    }

    [Fact]
    public void ReadBits_64Bits_ReadsFullUlong()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        var reader = new BitReader(data);
        reader.ReadBits(64).Should().Be(0x0102030405060708UL);
    }

    [Fact]
    public void ReadBits_ZeroCount_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var r = new BitReader([0x00]);
            r.ReadBits(0);
        });
    }

    [Fact]
    public void ReadBits_CountExceedsRemaining_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var r = new BitReader([0xFF]);
            r.ReadBits(8); // consume all
            r.ReadBits(1);
        });
    }

    [Fact]
    public void ReadBits_65Bits_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var r = new BitReader(new byte[9]);
            r.ReadBits(65);
        });
    }

    // ── ReadSignedBits ────────────────────────────────────────────────────────

    [Fact]
    public void ReadSignedBits_PositiveValue_NoSignExtension()
    {
        var reader = new BitReader([0x7F]); // 0111 1111 → +127
        reader.ReadSignedBits(8).Should().Be(127);
    }

    [Fact]
    public void ReadSignedBits_NegativeValue_SignExtended()
    {
        var reader = new BitReader([0x80]); // 1000 0000 → -128 as int8
        reader.ReadSignedBits(8).Should().Be(-128);
    }

    [Fact]
    public void ReadSignedBits_SubByte_SignExtendedCorrectly()
    {
        // 4-bit value: 0b1000 = -8 in two's complement
        var reader = new BitReader([0x80]); // 1000 xxxx
        reader.ReadSignedBits(4).Should().Be(-8);
    }

    [Fact]
    public void ReadSignedBits_AllOnes_IsMinusOne()
    {
        var reader = new BitReader([0xFF, 0xFF]);
        reader.ReadSignedBits(16).Should().Be(-1);
    }

    [Fact]
    public void ReadSignedBits_64Bits_NegativeValue()
    {
        byte[] data = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        var reader = new BitReader(data);
        reader.ReadSignedBits(64).Should().Be(-1L);
    }

    // ── ReadBool ──────────────────────────────────────────────────────────────

    [Fact]
    public void ReadBool_HighBitSet_ReturnsTrue()
    {
        var reader = new BitReader([0x80]);
        reader.ReadBool().Should().BeTrue();
    }

    [Fact]
    public void ReadBool_HighBitClear_ReturnsFalse()
    {
        var reader = new BitReader([0x40]);
        reader.ReadBool().Should().BeFalse();
    }

    // ── ReadBytes ─────────────────────────────────────────────────────────────

    [Fact]
    public void ReadBytes_Aligned_ReturnsSlice()
    {
        var reader = new BitReader([0x01, 0x02, 0x03]);
        var slice = reader.ReadBytes(2);
        slice.ToArray().Should().Equal(0x01, 0x02);
    }

    [Fact]
    public void ReadBytes_Unaligned_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var r = new BitReader([0xFF, 0x00]);
            r.ReadBits(3); // misalign
            r.ReadBytes(1);
        });
        ex.Message.Should().Contain("alignment");
    }

    // ── Position tracking ─────────────────────────────────────────────────────

    [Fact]
    public void ByteOffset_AfterReadingOneByte_IsOne()
    {
        var reader = new BitReader([0x00, 0x00]);
        reader.ReadBits(8);
        reader.ByteOffset.Should().Be(1);
    }

    [Fact]
    public void RemainingBits_AfterReads_DecrementsCorrectly()
    {
        var reader = new BitReader([0xFF, 0xFF]);
        reader.RemainingBits.Should().Be(16);
        reader.ReadBits(5);
        reader.RemainingBits.Should().Be(11);
    }

    // ── Skip ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Skip_AdvancesPosition()
    {
        var reader = new BitReader([0xF0, 0x0F]);
        reader.Skip(8);
        reader.ReadBits(8).Should().Be(0x0F);
    }

    [Fact]
    public void Skip_NegativeCount_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var r = new BitReader([0x00]);
            r.Skip(-1);
        });
    }

    // ── SetPosition ───────────────────────────────────────────────────────────

    [Fact]
    public void SetPosition_SeeksToAbsoluteBit()
    {
        var reader = new BitReader([0xAB, 0xCD]);
        reader.ReadBits(8); // advance past first byte
        reader.SetPosition(0); // rewind
        reader.ReadBits(8).Should().Be(0xAB);
    }

    [Fact]
    public void SetPosition_OutOfBounds_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var r = new BitReader([0x00]);
            r.SetPosition(9);
        });
    }

    // ── AlignToByte ───────────────────────────────────────────────────────────

    [Fact]
    public void AlignToByte_WhenMisaligned_RoundsUp()
    {
        var reader = new BitReader([0xFF, 0xAB]);
        reader.ReadBits(3);
        reader.AlignToByte();
        reader.BitPosition.Should().Be(8);
        reader.ReadBits(8).Should().Be(0xAB);
    }

    [Fact]
    public void AlignToByte_WhenAligned_IsNoop()
    {
        var reader = new BitReader([0xAB]);
        reader.AlignToByte();
        reader.BitPosition.Should().Be(0);
    }
}
