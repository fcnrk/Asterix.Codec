using Asterix.Codec.Binary;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class StringEncoderTests
{
    // ── IA5 decode ────────────────────────────────────────────────────────────

    [Fact]
    public void DecodeIa5_Callsign_BAW123()
    {
        // Pre-computed IA5 encoding of "BAW123  " (8 chars, 2 trailing spaces → trimmed)
        // B=2,A=1,W=23,1=49,2=50,3=51,sp=32,sp=32 packed 6-bit MSB-first
        byte[] bytes = [0x08, 0x15, 0xF1, 0xCB, 0x38, 0x20];
        StringEncoders.DecodeIa5(bytes).Should().Be("BAW123");
    }

    [Fact]
    public void DecodeIa5_AllSpaces_ReturnsEmpty()
    {
        // All-space 6-bit codes = 0x20 packed → every 6-bit group = 32 → trimmed to ""
        byte[] bytes = [0x82, 0x08, 0x20, 0x82, 0x08, 0x20];
        // All chars decoded as space → trimmed → ""
        StringEncoders.DecodeIa5(bytes).Should().Be(string.Empty);
    }

    [Fact]
    public void DecodeIa5_SingleChar_A()
    {
        // 'A' = code 1 = 000001, padded to byte: 000001xx → 0x04, then zeros
        // 1 byte = 1 char. Code: 000001|xx = 0x04 (top 6 bits)
        byte[] bytes = [0x04]; // 000001|00 → code=1 → 'A', second char would be code 0 → space → trimmed
        // Wait: 1 byte = 8 bits = 1 full 6-bit code + 2 leftover bits (incomplete char)
        // 1 byte → 8/6=1 full char only
        StringEncoders.DecodeIa5(bytes).Should().Be("A");
    }

    [Fact]
    public void DecodeIa5_EmptySpan_ReturnsEmpty()
    {
        StringEncoders.DecodeIa5([]).Should().Be(string.Empty);
    }

    // ── IA5 encode ────────────────────────────────────────────────────────────

    [Fact]
    public void EncodeIa5_Callsign_BAW123_MatchesExpected()
    {
        var writer = new BitWriter();
        StringEncoders.EncodeIa5("BAW123", byteLength: 6, writer);
        writer.ToArray().Should().Equal(0x08, 0x15, 0xF1, 0xCB, 0x38, 0x20);
    }

    [Fact]
    public void EncodeIa5_TooLong_ThrowsArgumentException()
    {
        var writer = new BitWriter();
        // 6 bytes = 8 max chars; "ABCDEFGHI" is 9 chars
        writer.Invoking(w => StringEncoders.EncodeIa5("ABCDEFGHI", 6, w))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EncodeDecodeIa5_RoundTrip_PreservesCallsign()
    {
        string original = "EZY12AB";
        var writer = new BitWriter();
        StringEncoders.EncodeIa5(original, byteLength: 6, writer);

        string decoded = StringEncoders.DecodeIa5(writer.ToSpan());
        decoded.Should().Be(original);
    }

    [Fact]
    public void EncodeIa5_Lowercase_ConvertsToUppercase()
    {
        // IA5 CharToIa5Code maps a-z → uppercase 6-bit codes
        var writer = new BitWriter();
        StringEncoders.EncodeIa5("baw", byteLength: 6, writer);

        // Decoded result should be "BAW" (uppercased by 6-bit mapping)
        string decoded = StringEncoders.DecodeIa5(writer.ToSpan());
        decoded.Should().Be("BAW");
    }

    // ── ASCII decode ──────────────────────────────────────────────────────────

    [Fact]
    public void DecodeAscii_SimpleString_ReturnsString()
    {
        byte[] bytes = [0x54, 0x45, 0x53, 0x54]; // "TEST"
        StringEncoders.DecodeAscii(bytes).Should().Be("TEST");
    }

    [Fact]
    public void DecodeAscii_TrailingNulls_Stripped()
    {
        byte[] bytes = [0x48, 0x49, 0x00, 0x00]; // "HI\0\0"
        StringEncoders.DecodeAscii(bytes).Should().Be("HI");
    }

    [Fact]
    public void DecodeAscii_TrailingSpaces_Stripped()
    {
        byte[] bytes = [0x48, 0x49, 0x20, 0x20]; // "HI  "
        StringEncoders.DecodeAscii(bytes).Should().Be("HI");
    }

    [Fact]
    public void DecodeAscii_AllNulls_ReturnsEmpty()
    {
        StringEncoders.DecodeAscii([0x00, 0x00]).Should().Be(string.Empty);
    }

    // ── ASCII encode ──────────────────────────────────────────────────────────

    [Fact]
    public void EncodeAscii_ShortString_NullPadded()
    {
        var writer = new BitWriter();
        StringEncoders.EncodeAscii("HI", byteLength: 4, writer);
        writer.ToArray().Should().Equal(0x48, 0x49, 0x00, 0x00);
    }

    [Fact]
    public void EncodeAscii_TooLong_ThrowsArgumentException()
    {
        var writer = new BitWriter();
        writer.Invoking(w => StringEncoders.EncodeAscii("TOOLONG", 4, w))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EncodeDecodeAscii_RoundTrip()
    {
        string original = "TEST";
        var writer = new BitWriter();
        StringEncoders.EncodeAscii(original, 4, writer);
        StringEncoders.DecodeAscii(writer.ToSpan()).Should().Be(original);
    }
}
