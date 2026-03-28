using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using Asterix.Codec.Encode;
using Asterix.Codec.Model;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.CustomSchema;

/// <summary>
/// Tests for the generic SPF decoder and encoder using the programmatic version of
/// <c>spf_custom_062.yml</c>. Verifies the mandatory decode order
/// (length → count → repetitive → presence → conditional) and SPF round-trip correctness.
/// </summary>
public class SpfCustom062Tests
{
    // ── Decode ────────────────────────────────────────────────────────────────

    [Fact]
    public void Decode_LengthField_IsCorrect()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetScalar("length").Should().Be(19UL);
    }

    [Fact]
    public void Decode_CountField_IsCorrect()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetScalar("f1RecordCount").Should().Be(2UL);
    }

    [Fact]
    public void Decode_RepetitiveF1_TwoElements()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        var f1 = item.GetRepetitive("f1");
        f1.Should().NotBeNull();
        f1!.Should().HaveCount(2);
    }

    [Fact]
    public void Decode_RepetitiveF1_Element0_CorrectFields()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        var el0 = item.GetRepetitive("f1")![0];
        el0.GetField("f2")!.RawValue.Should().Be(10UL);
        el0.GetField("f3")!.RawValue.Should().Be(11UL);
    }

    [Fact]
    public void Decode_RepetitiveF1_Element1_CorrectFields()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        var el1 = item.GetRepetitive("f1")![1];
        el1.GetField("f2")!.RawValue.Should().Be(12UL);
        el1.GetField("f3")!.RawValue.Should().Be(13UL);
    }

    [Fact]
    public void Decode_PresenceFlags_CorrectValues()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        var flags = item.GetPresenceFlags("presence");
        flags.Should().NotBeNull();
        flags!["f4"].Should().Be(1UL);  // present
        flags!["f5"].Should().Be(0UL);  // absent
        flags!["f6"].Should().Be(1UL);  // present
        flags!["f7"].Should().Be(0UL);  // absent
        flags!["f8"].Should().Be(1UL);  // present
    }

    [Fact]
    public void Decode_OptionalF4_PresentAndCorrect()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetOptional("f4")!.RawValue.Should().Be(66UL);
    }

    [Fact]
    public void Decode_OptionalF5_AbsentReturnsNull()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetOptional("f5").Should().BeNull();
    }

    [Fact]
    public void Decode_OptionalF6_PresentWithCorrectUint16()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetOptional("f6")!.RawValue.Should().Be(0x1234UL);
    }

    [Fact]
    public void Decode_OptionalF7_AbsentReturnsNull()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetOptional("f7").Should().BeNull();
    }

    [Fact]
    public void Decode_OptionalF8_PresentWithCorrectAsciiString()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetOptional("f8")!.StringValue.Should().Be("TEST");
    }

    [Fact]
    public void Decode_ConsumesExactlyBlockLength_ReaderAtEnd()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);

        SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        reader.RemainingBits.Should().Be(0,
            "SPF decoder must consume exactly the bytes declared in the length field");
    }

    // ── SPF round-trip ────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_SpfBlock_ByteForByte()
    {
        var definition = SchemaFixtures.SpfCustom062();

        // Decode
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);
        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        // Re-encode
        var writer = new BitWriter();
        SpfEncoder.Encode(writer, item, definition);

        writer.ToArray().Should().Equal(PayloadFixtures.SpfCustom062Block);
    }

    [Fact]
    public void RoundTrip_SpfBlock_LengthFieldConsistentWithPayload()
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(PayloadFixtures.SpfCustom062Block);
        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        var writer = new BitWriter();
        SpfEncoder.Encode(writer, item, definition);

        byte[] result = writer.ToArray();
        int encodedLength = result.Length;

        // First 2 bytes are the length field value (uint16 big-endian)
        int declaredLength = (result[0] << 8) | result[1];
        declaredLength.Should().Be(encodedLength,
            "length field must equal the total encoded byte count");
    }

    // ── Decode order enforcement ──────────────────────────────────────────────

    [Fact]
    public void Decode_AllOptionalFieldsAbsent_NoNullReferenceException()
    {
        // Build a minimal SPF block: length=7, count=0, no F1 elements, all presence=0, no optionals
        // length(2) + f1RecordCount(1) + presence×5(5) = 8 bytes, length field = 8
        byte[] minimal =
        [
            0x00, 0x08,         // length = 8
            0x00,               // f1RecordCount = 0
            0x00, 0x00, 0x00, 0x00, 0x00, // presence f4..f8 = all absent
        ];

        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(minimal);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, definition, DecodeMode.Strict);

        item.GetRepetitive("f1")!.Should().BeEmpty();
        item.GetOptional("f4").Should().BeNull();
        item.GetOptional("f5").Should().BeNull();
        item.GetOptional("f6").Should().BeNull();
        item.GetOptional("f7").Should().BeNull();
        item.GetOptional("f8").Should().BeNull();
    }
}
