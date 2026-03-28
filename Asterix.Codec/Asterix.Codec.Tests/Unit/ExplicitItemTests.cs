using Asterix.Codec.Binary;
using Asterix.Codec.Decode.ItemDecoders;
using Asterix.Codec.Encode.ItemEncoders;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

/// <summary>
/// Tests for explicit (RE / SP) item decode, encode, and round-trip.
///
/// Wire format:  [LEN][content bytes…]
/// LEN includes the length byte itself, so:
///   LEN=1 → 0 content bytes
///   LEN=3 → 2 content bytes (0xAB, 0xCD)
/// </summary>
public class ExplicitItemTests
{
    private static readonly ExplicitItemDefinition Def = new();

    // ── Decode ────────────────────────────────────────────────────────────────

    [Fact]
    public void Decode_WithContent_ContentBytesCorrect()
    {
        // LEN=3: 1 length byte + 2 content bytes
        byte[] data = [0x03, 0xAB, 0xCD];
        var reader = new BitReader(data);

        ExplicitDecodedItem item = ExplicitItemDecoder.Decode(ref reader, "TEST");

        item.Content.Should().Equal(0xAB, 0xCD);
    }

    [Fact]
    public void Decode_LenOne_EmptyContent()
    {
        byte[] data = [0x01]; // LEN=1 → 0 content bytes
        var reader = new BitReader(data);

        ExplicitDecodedItem item = ExplicitItemDecoder.Decode(ref reader, "TEST");

        item.Content.Should().BeEmpty();
    }

    [Fact]
    public void Decode_ConsumesExactlyLenBytes()
    {
        byte[] data = [0x03, 0xAB, 0xCD, 0xFF]; // 0xFF is sentinel
        var reader = new BitReader(data);

        ExplicitItemDecoder.Decode(ref reader, "TEST");

        reader.ByteOffset.Should().Be(3, "LEN=3 → 3 bytes consumed");
        reader.ReadBits(8).Should().Be(0xFF);
    }

    [Fact]
    public void Decode_LenZero_Throws()
    {
        byte[] data = [0x00];
        Assert.Throws<Exceptions.DecodeException>(() =>
        {
            var r = new BitReader(data);
            ExplicitItemDecoder.Decode(ref r, "TEST");
        });
    }

    [Fact]
    public void Decode_LenExceedsData_Throws()
    {
        // LEN=5 but only 3 bytes total (1 len + 1 content)
        byte[] data = [0x05, 0xAB];
        Assert.Throws<Exceptions.DecodeException>(() =>
        {
            var r = new BitReader(data);
            ExplicitItemDecoder.Decode(ref r, "TEST");
        });
    }

    [Fact]
    public void Decode_LargePayload_AllBytesPreserved()
    {
        byte[] content = Enumerable.Range(0, 254).Select(i => (byte)i).ToArray();
        byte[] data = [(byte)(content.Length + 1), ..content]; // LEN = 255

        var reader = new BitReader(data);
        ExplicitDecodedItem item = ExplicitItemDecoder.Decode(ref reader, "TEST");

        item.Content.Should().Equal(content);
    }

    // ── Encode ────────────────────────────────────────────────────────────────

    [Fact]
    public void Encode_WithContent_LenByteAndContentWritten()
    {
        var item = new ExplicitDecodedItem([0xAB, 0xCD]);
        var writer = new BitWriter();
        ExplicitItemEncoder.Encode(writer, item, Def, "TEST");

        writer.ToArray().Should().Equal(0x03, 0xAB, 0xCD);
    }

    [Fact]
    public void Encode_EmptyContent_OnlyLenByte()
    {
        var item = new ExplicitDecodedItem([]);
        var writer = new BitWriter();
        ExplicitItemEncoder.Encode(writer, item, Def, "TEST");

        writer.ToArray().Should().Equal(0x01); // LEN = 0+1 = 1
    }

    [Fact]
    public void Encode_LenByteIsContentLengthPlusOne()
    {
        byte[] content = [0x10, 0x20, 0x30];
        var item = new ExplicitDecodedItem(content);
        var writer = new BitWriter();
        ExplicitItemEncoder.Encode(writer, item, Def, "TEST");

        byte[] encoded = writer.ToArray();
        encoded[0].Should().Be((byte)(content.Length + 1));
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_ByteForByte()
    {
        byte[] original = [0x03, 0xAB, 0xCD];
        var reader = new BitReader(original);
        ExplicitDecodedItem decoded = ExplicitItemDecoder.Decode(ref reader, "TEST");

        var writer = new BitWriter();
        ExplicitItemEncoder.Encode(writer, decoded, Def, "TEST");

        writer.ToArray().Should().Equal(original);
    }

    [Fact]
    public void RoundTrip_LenOneByteForByte()
    {
        byte[] original = [0x01];
        var reader = new BitReader(original);
        ExplicitDecodedItem decoded = ExplicitItemDecoder.Decode(ref reader, "TEST");

        var writer = new BitWriter();
        ExplicitItemEncoder.Encode(writer, decoded, Def, "TEST");

        writer.ToArray().Should().Equal(original);
    }
}
