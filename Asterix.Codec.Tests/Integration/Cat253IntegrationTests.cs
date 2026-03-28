using Asterix.Codec.Decode;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Integration;

/// <summary>
/// End-to-end decode, encode, and round-trip tests for CAT253 —
/// a discriminated multi-message category with a structured-explicit application data item.
/// Uses the YAML sample files cat253.yml + structured_explicit_cat253.yml.
/// </summary>
public class Cat253IntegrationTests
{
    private static string SamplesPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "samples", file);

    private static AsterixCodec BuildCodec(DecodeMode mode = DecodeMode.Strict) =>
        new AsterixCodecBuilder()
            .AddCategoryFromYaml(SamplesPath("cat253.yml"))
            .AddStructuredExplicitItemsFromYaml(SamplesPath("structured_explicit_cat253.yml"))
            .WithMode(mode)
            .Build();

    // ── Type 001 — status record ───────────────────────────────────────────────

    [Fact]
    public void Decode_Cat253_Type001_PacketCategoryAndRecordCount()
    {
        var packet = BuildCodec().Decode(PayloadFixtures.Cat253Type001);

        packet.Category.Should().Be(253);
        packet.Records.Should().HaveCount(1);
    }

    [Fact]
    public void Decode_Cat253_Type001_DiscriminatorItemPresent()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type001).Records[0];

        record.Items.Should().ContainKey("I253_010");
        var disc = record.Items["I253_010"].Should().BeOfType<FixedDecodedItem>().Subject;
        disc.GetField("message_type")!.RawValue.Should().Be(1UL);
    }

    [Fact]
    public void Decode_Cat253_Type001_StatusFieldDecoded()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type001).Records[0];

        var i001 = record.Items["I253_001"].Should().BeOfType<FixedDecodedItem>().Subject;
        i001.GetField("status")!.RawValue.Should().Be(42UL);
    }

    // ── Type 100 — structured-explicit container ─────────────────────────

    [Fact]
    public void Decode_Cat253_Type100_I253_100_IsStructuredExplicitDecodedItem()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];

        record.Items["I253_100"].Should().BeOfType<StructuredExplicitDecodedItem>();
    }

    [Fact]
    public void Decode_Cat253_Type100_StructuredExplicitItemHasFourInnerItems()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];
        var seItem = (StructuredExplicitDecodedItem)record.Items["I253_100"];

        seItem.Items.Should().HaveCount(4);
        seItem.Items.Keys.Should().BeEquivalentTo(
            new[] { "position", "transponder", "measurements", "nav_data" });
    }

    [Fact]
    public void Decode_Cat253_Type100_PositionFields()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];
        var seItem = (StructuredExplicitDecodedItem)record.Items["I253_100"];
        var position = (FixedDecodedItem)seItem.Items["position"];

        position.GetField("track_id")!.RawValue.Should().Be(7UL);
        position.GetField("latitude")!.RawValue.Should().Be(256UL);
        position.GetField("longitude")!.RawValue.Should().Be(512UL);
    }

    [Fact]
    public void Decode_Cat253_Type100_TransponderSingleGroup_CorrectFields()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];
        var seItem = (StructuredExplicitDecodedItem)record.Items["I253_100"];
        var transponder = (VariableDecodedItem)seItem.Items["transponder"];

        transponder.Groups.Should().HaveCount(1);
        transponder.GetField("alert")!.RawValue.Should().Be(0UL);
        transponder.GetField("spi")!.RawValue.Should().Be(0UL);
        transponder.GetField("squawk")!.RawValue.Should().Be(5UL);
        transponder.GetField("spare")!.RawValue.Should().Be(0UL);
    }

    [Fact]
    public void Decode_Cat253_Type100_MeasurementsTwoElements()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];
        var seItem = (StructuredExplicitDecodedItem)record.Items["I253_100"];
        var measurements = (RepetitiveDecodedItem)seItem.Items["measurements"];

        measurements.Count.Should().Be(2);

        var elem0 = (FixedDecodedItem)measurements.Elements[0];
        elem0.GetField("sensor_id")!.RawValue.Should().Be(1UL);
        elem0.GetField("quality")!.RawValue.Should().Be(100UL);
        elem0.GetField("range")!.RawValue.Should().Be(50UL);

        var elem1 = (FixedDecodedItem)measurements.Elements[1];
        elem1.GetField("sensor_id")!.RawValue.Should().Be(2UL);
        elem1.GetField("quality")!.RawValue.Should().Be(80UL);
        elem1.GetField("range")!.RawValue.Should().Be(30UL);
    }

    [Fact]
    public void Decode_Cat253_Type100_NavDataAltAndSpdPresent()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];
        var seItem = (StructuredExplicitDecodedItem)record.Items["I253_100"];
        var navData = (CompoundDecodedItem)seItem.Items["nav_data"];

        navData.Subitems.Should().ContainKey("nav_data/ALT");
        navData.Subitems.Should().ContainKey("nav_data/SPD");
        navData.Subitems.Should().NotContainKey("nav_data/HDG");
    }

    [Fact]
    public void Decode_Cat253_Type100_NavDataAltScaled()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];
        var seItem = (StructuredExplicitDecodedItem)record.Items["I253_100"];
        var navData = (CompoundDecodedItem)seItem.Items["nav_data"];

        var alt = (FixedDecodedItem)navData.Subitems["nav_data/ALT"];
        alt.GetField("altitude")!.RawValue.Should().Be(4000UL);
        alt.GetField("altitude")!.ScaledValue.Should().BeApproximately(1000.0, precision: 1e-9);
    }

    [Fact]
    public void Decode_Cat253_Type100_NavDataSpdScaled()
    {
        var record = BuildCodec().Decode(PayloadFixtures.Cat253Type100).Records[0];
        var seItem = (StructuredExplicitDecodedItem)record.Items["I253_100"];
        var navData = (CompoundDecodedItem)seItem.Items["nav_data"];

        var spd = (FixedDecodedItem)navData.Subitems["nav_data/SPD"];
        spd.GetField("speed")!.RawValue.Should().Be(25000UL);
        spd.GetField("speed")!.ScaledValue.Should().BeApproximately(250.0, precision: 1e-9);
    }

    // ── Discriminator error handling ───────────────────────────────────────────

    [Fact]
    public void Decode_Cat253_UnknownDiscriminatorValue_StrictMode_ThrowsDecodeException()
    {
        // Build a packet with message_type=99 (no matching message definition).
        byte[] badPacket =
        [
            0xFD, 0x00, 0x07,   // CAT=253, LEN=7
            0xC0,               // FSPEC: I253_010, I253_001
            0x63,               // I253_010: message_type=99 (unknown)
            0x00, 0x00,         // I253_001 placeholder bytes
        ];

        var act = () => BuildCodec(DecodeMode.Strict).Decode(badPacket);

        act.Should().Throw<DecodeException>();
    }

    [Fact]
    public void Decode_Cat253_UnknownDiscriminatorValue_LenientMode_DoesNotThrow()
    {
        // Build a packet with message_type=99 (no matching message definition).
        byte[] badPacket =
        [
            0xFD, 0x00, 0x07,   // CAT=253, LEN=7
            0xC0,               // FSPEC: I253_010, I253_001
            0x63,               // I253_010: message_type=99 (unknown)
            0x00, 0x00,         // payload bytes
        ];

        var act = () => BuildCodec(DecodeMode.Lenient).Decode(badPacket);

        act.Should().NotThrow();
    }

    // ── Round-trip ─────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Cat253_Type001_ByteForByte()
    {
        BuildCodec().RoundTrip(PayloadFixtures.Cat253Type001)
            .Should().Equal(PayloadFixtures.Cat253Type001);
    }

    [Fact]
    public void RoundTrip_Cat253_Type100_ByteForByte()
    {
        BuildCodec().RoundTrip(PayloadFixtures.Cat253Type100)
            .Should().Equal(PayloadFixtures.Cat253Type100);
    }
}
