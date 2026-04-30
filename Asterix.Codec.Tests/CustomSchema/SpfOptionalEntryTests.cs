using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using Asterix.Codec.Encode;
using Asterix.Codec.Model;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.CustomSchema;

public class SpfOptionalEntryTests
{
    // ── optional_group — present ──────────────────────────────────────────────

    [Fact]
    public void Decode_OptionalGroup_Present_ReturnsSpfGroupValue()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        item.GetOptionalGroup("grp1").Should().NotBeNull();
    }

    [Fact]
    public void Decode_OptionalGroup_Present_CorrectFieldGa()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        item.GetOptionalGroup("grp1")!.GetField("ga")!.RawValue.Should().Be(0xAAUL);
    }

    [Fact]
    public void Decode_OptionalGroup_Present_CorrectFieldGb()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        item.GetOptionalGroup("grp1")!.GetField("gb")!.RawValue.Should().Be(0xBBCCUL);
    }

    // ── optional_group — absent ───────────────────────────────────────────────

    [Fact]
    public void Decode_OptionalGroup_Absent_ReturnsNull()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedAbsent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        item.GetOptionalGroup("grp1").Should().BeNull();
    }

    // ── optional_repetitive — present ────────────────────────────────────────

    [Fact]
    public void Decode_OptionalRepetitive_Present_ReturnsValue()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        item.GetOptionalRepetitive("rep1").Should().NotBeNull();
    }

    [Fact]
    public void Decode_OptionalRepetitive_Present_CountIsCorrect()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        item.GetOptionalRepetitive("rep1")!.Count.Should().Be(2);
    }

    [Fact]
    public void Decode_OptionalRepetitive_Present_Element0_Correct()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        var el0 = item.GetOptionalRepetitive("rep1")!.Elements[0];
        el0.GetField("ra")!.RawValue.Should().Be(0x11UL);
        el0.GetField("rb")!.RawValue.Should().Be(0x22UL);
    }

    [Fact]
    public void Decode_OptionalRepetitive_Present_Element1_Correct()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        var el1 = item.GetOptionalRepetitive("rep1")!.Elements[1];
        el1.GetField("ra")!.RawValue.Should().Be(0x33UL);
        el1.GetField("rb")!.RawValue.Should().Be(0x44UL);
    }

    // ── optional_repetitive — absent ─────────────────────────────────────────

    [Fact]
    public void Decode_OptionalRepetitive_Absent_ReturnsNull()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedAbsent);

        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        item.GetOptionalRepetitive("rep1").Should().BeNull();
    }

    // ── length boundary ───────────────────────────────────────────────────────

    [Fact]
    public void Decode_Present_ConsumesExactlyBlockLength()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);

        SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        reader.RemainingBits.Should().Be(0);
    }

    [Fact]
    public void Decode_Absent_ConsumesExactlyBlockLength()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedAbsent);

        SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        reader.RemainingBits.Should().Be(0);
    }

    // ── round-trip — all present ──────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Present_ByteForByte()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);
        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        var writer = new Asterix.Codec.Binary.BitWriter();
        SpfEncoder.Encode(writer, item, def);

        writer.ToArray().Should().Equal(PayloadFixtures.SpfExtendedPresent);
    }

    [Fact]
    public void RoundTrip_Present_LengthFieldConsistentWithPayload()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedPresent);
        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        var writer = new Asterix.Codec.Binary.BitWriter();
        SpfEncoder.Encode(writer, item, def);

        byte[] result = writer.ToArray();
        int declaredLength = (result[0] << 8) | result[1];
        declaredLength.Should().Be(result.Length);
    }

    // ── round-trip — all absent ───────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Absent_ByteForByte()
    {
        var def = SchemaFixtures.SpfExtended();
        var reader = new BitReader(PayloadFixtures.SpfExtendedAbsent);
        SpfDecodedItem item = SpfDecoder.Decode(ref reader, def, DecodeMode.Strict);

        var writer = new Asterix.Codec.Binary.BitWriter();
        SpfEncoder.Encode(writer, item, def);

        writer.ToArray().Should().Equal(PayloadFixtures.SpfExtendedAbsent);
    }

    // ── EncodePresence fallback — programmatically constructed item ───────────

    [Fact]
    public void EncodePresence_Fallback_OptionalGroup_SetsFlagToOne()
    {
        var grp1 = new SpfGroupValue([
            new DecodedField("ga", rawValue: 0xAAUL, scaledValue: null, stringValue: null),
            new DecodedField("gb", rawValue: 0xBBCCUL, scaledValue: null, stringValue: null),
        ]);

        var fields = new Dictionary<string, object?>
        {
            ["length"]   = 0UL,
            ["presence"] = null,  // no stored flags dict → fallback path
            ["grp1"]     = grp1,
            ["rep1"]     = null,
        };
        var item = new SpfDecodedItem(fields);
        var def = SchemaFixtures.SpfExtended();

        var writer = new Asterix.Codec.Binary.BitWriter();
        SpfEncoder.Encode(writer, item, def);

        byte[] result = writer.ToArray();
        // layout: length(2), presence.grp1(1), presence.rep1(1), grp1.ga(1), grp1.gb(2)
        result[2].Should().Be(1, "presence flag for grp1 should be 1 (present)");
        result[3].Should().Be(0, "presence flag for rep1 should be 0 (absent)");
    }

    [Fact]
    public void EncodePresence_Fallback_OptionalRepetitive_SetsFlagToOne()
    {
        var rep1 = new SpfOptionalRepetitiveValue(1,
        [
            new SpfGroupValue([
                new DecodedField("ra", rawValue: 0x11UL, scaledValue: null, stringValue: null),
                new DecodedField("rb", rawValue: 0x22UL, scaledValue: null, stringValue: null),
            ])
        ]);

        var fields = new Dictionary<string, object?>
        {
            ["length"]   = 0UL,
            ["presence"] = null,
            ["grp1"]     = null,
            ["rep1"]     = rep1,
        };
        var item = new SpfDecodedItem(fields);
        var def = SchemaFixtures.SpfExtended();

        var writer = new Asterix.Codec.Binary.BitWriter();
        SpfEncoder.Encode(writer, item, def);

        byte[] result = writer.ToArray();
        result[2].Should().Be(0, "presence flag for grp1 should be 0 (absent)");
        result[3].Should().Be(1, "presence flag for rep1 should be 1 (present)");
    }
}
