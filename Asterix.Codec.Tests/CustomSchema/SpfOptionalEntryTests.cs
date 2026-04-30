using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
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
}
