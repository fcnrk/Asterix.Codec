using Asterix.Codec.Decode;
using Asterix.Codec.Model;
using Asterix.Codec.Schema;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Integration;

/// <summary>
/// End-to-end decode tests against hand-crafted CAT062 binary payloads.
/// Schemas are built programmatically via <see cref="SchemaFixtures"/> since
/// <c>YamlSchemaLoader</c> is not yet implemented (Phase 3).
/// </summary>
public class Cat062DecodeTests
{
    private static AsterixDecoder BuildDecoder(DecodeMode mode = DecodeMode.Strict)
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        return new AsterixDecoder(registry, mode);
    }

    // ── Fixed items ───────────────────────────────────────────────────────────

    [Fact]
    public void Decode_FixedItems_I062_010_And_I062_040()
    {
        var packet = BuildDecoder().Decode(PayloadFixtures.Cat062Simple);

        packet.Category.Should().Be(62);
        packet.Records.Should().HaveCount(1);

        DecodedRecord record = packet.Records[0];
        record.Items.Should().ContainKey("I062_010");
        record.Items.Should().ContainKey("I062_040");

        var item010 = record.Items["I062_010"].Should().BeOfType<FixedDecodedItem>().Subject;
        item010.GetField("sac")!.RawValue.Should().Be(1);
        item010.GetField("sic")!.RawValue.Should().Be(2);

        var item040 = record.Items["I062_040"].Should().BeOfType<FixedDecodedItem>().Subject;
        item040.GetField("track_number")!.RawValue.Should().Be(0x1234UL);
    }

    [Fact]
    public void Decode_FixedItem_WithScale_I062_070_TimeIsScaled()
    {
        var packet = BuildDecoder().Decode(PayloadFixtures.Cat062WithTime);

        var item070 = packet.Records[0].Items["I062_070"]
            .Should().BeOfType<FixedDecodedItem>().Subject;

        var timeField = item070.GetField("time")!;
        timeField.RawValue.Should().Be(9600UL);
        timeField.ScaledValue.Should().BeApproximately(75.0, precision: 1e-10);
    }

    // ── IA5 string ────────────────────────────────────────────────────────────

    [Fact]
    public void Decode_FixedItem_IA5String_I062_245_Callsign()
    {
        var packet = BuildDecoder().Decode(PayloadFixtures.Cat062WithCallsign);

        var item245 = packet.Records[0].Items["I062_245"]
            .Should().BeOfType<FixedDecodedItem>().Subject;

        item245.GetField("callsign")!.StringValue.Should().Be("BAW123");
    }

    // ── Compound item ─────────────────────────────────────────────────────────

    [Fact]
    public void Decode_CompoundItem_I062_210_OnlyPresentSubitemsDecoded()
    {
        var packet = BuildDecoder().Decode(PayloadFixtures.Cat062WithCompound);
        var item210 = packet.Records[0].Items["I062_210"]
            .Should().BeOfType<CompoundDecodedItem>().Subject;

        item210.Subitems.Should().ContainKey("qx");
        item210.Subitems.Should().ContainKey("qy");
        item210.Subitems.Should().NotContainKey("qvx");

        var qx = item210.Subitems["qx"].Should().BeOfType<FixedDecodedItem>().Subject;
        qx.GetField("value")!.RawValue.Should().Be(4UL);
        qx.GetField("value")!.ScaledValue.Should().BeApproximately(1.0, precision: 1e-10);

        var qy = item210.Subitems["qy"].Should().BeOfType<FixedDecodedItem>().Subject;
        qy.GetField("value")!.RawValue.Should().Be(8UL);
        qy.GetField("value")!.ScaledValue.Should().BeApproximately(2.0, precision: 1e-10);
    }

    // ── Repetitive item ───────────────────────────────────────────────────────

    [Fact]
    public void Decode_RepetitiveItem_I062_290_TwoElements()
    {
        var packet = BuildDecoder().Decode(PayloadFixtures.Cat062WithRepetitive);
        var item290 = packet.Records[0].Items["I062_290"]
            .Should().BeOfType<RepetitiveDecodedItem>().Subject;

        item290.Elements.Should().HaveCount(2);

        var el0 = item290.Elements[0].Should().BeOfType<FixedDecodedItem>().Subject;
        el0.GetField("age")!.RawValue.Should().Be(256UL);
        el0.GetField("age")!.ScaledValue.Should().BeApproximately(2.0, precision: 1e-10);

        var el1 = item290.Elements[1].Should().BeOfType<FixedDecodedItem>().Subject;
        el1.GetField("age")!.RawValue.Should().Be(512UL);
        el1.GetField("age")!.ScaledValue.Should().BeApproximately(4.0, precision: 1e-10);
    }

    // ── Header validation ─────────────────────────────────────────────────────

    [Fact]
    public void Decode_ExactLengthMatch_Succeeds()
    {
        // Cat062Simple LEN=8 exactly matches data length → no error
        var act = () => BuildDecoder().Decode(PayloadFixtures.Cat062Simple);
        act.Should().NotThrow();
    }

    [Fact]
    public void Decode_LenExceedsData_Throws()
    {
        BuildDecoder().Invoking(d => d.Decode(PayloadFixtures.LenExceedsData))
            .Should().Throw<Exceptions.DecodeException>();
    }

    // ── Lenient mode ──────────────────────────────────────────────────────────

    [Fact]
    public void Decode_UnknownCategory_LenientMode_ReturnsEmptyPacket()
    {
        // Register nothing — CAT062 unknown in this registry
        var registry = new SchemaRegistry();
        registry.Freeze();
        var decoder = new AsterixDecoder(registry, DecodeMode.Lenient);

        var packet = decoder.Decode(PayloadFixtures.Cat062Simple);
        packet.Category.Should().Be(62);
        packet.Records.Should().BeEmpty();
    }

    [Fact]
    public void Decode_UnknownCategory_StrictMode_Throws()
    {
        var registry = new SchemaRegistry();
        registry.Freeze();
        var decoder = new AsterixDecoder(registry, DecodeMode.Strict);

        decoder.Invoking(d => d.Decode(PayloadFixtures.Cat062Simple))
            .Should().Throw<Exceptions.DecodeException>();
    }
}
