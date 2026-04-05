using Asterix.Codec.Decode;
using Asterix.Codec.Model;
using Asterix.Codec.Schema;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Integration;

public class FspecRepetitiveIntegrationTests
{
    private static AsterixDecoder BuildDecoder()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        return new AsterixDecoder(registry, DecodeMode.Strict);
    }

    // ── Decode ────────────────────────────────────────────────────────────────

    [Fact]
    public void Decode_I062_510_TwoEntries_CorrectElements()
    {
        var packet = BuildDecoder().Decode(PayloadFixtures.Cat062WithFspecRepetitive);

        packet.Category.Should().Be(62);
        packet.Records.Should().HaveCount(1);

        var item510 = packet.Records[0].Items["I062_510"]
            .Should().BeOfType<FspecRepetitiveDecodedItem>().Subject;

        item510.Count.Should().Be(2);

        var el0 = item510.Elements[0].Should().BeOfType<FixedDecodedItem>().Subject;
        el0.GetField("sac")!.RawValue.Should().Be(1UL);
        el0.GetField("sic")!.RawValue.Should().Be(2UL);
        el0.GetField("track_number")!.RawValue.Should().Be(256UL);

        var el1 = item510.Elements[1].Should().BeOfType<FixedDecodedItem>().Subject;
        el1.GetField("sac")!.RawValue.Should().Be(3UL);
        el1.GetField("sic")!.RawValue.Should().Be(4UL);
        el1.GetField("track_number")!.RawValue.Should().Be(512UL);
    }

    // ── Encode ────────────────────────────────────────────────────────────────

    [Fact]
    public void Encode_I062_510_TwoEntries_MatchesExpectedBytes()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        var codec = new AsterixCodec(
            new AsterixDecoder(registry, DecodeMode.Strict),
            new Asterix.Codec.Encode.AsterixEncoder(registry));

        var record = new DecodedRecord(new Dictionary<string, DecodedItem>
        {
            ["I062_010"] = new FixedDecodedItem(
            [
                new DecodedField("sac", 1, null, null),
                new DecodedField("sic", 2, null, null),
            ]),
            ["I062_510"] = new FspecRepetitiveDecodedItem(
            [
                new FixedDecodedItem(
                [
                    new DecodedField("sac",          1,   null, null),
                    new DecodedField("sic",          2,   null, null),
                    new DecodedField("track_number", 256, null, null),
                ]),
                new FixedDecodedItem(
                [
                    new DecodedField("sac",          3,   null, null),
                    new DecodedField("sic",          4,   null, null),
                    new DecodedField("track_number", 512, null, null),
                ]),
            ]),
        });

        byte[] encoded = codec.Encode(new AsterixPacket(62, [record]));
        encoded.Should().Equal(PayloadFixtures.Cat062WithFspecRepetitive);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_I062_510_TwoEntries_IsByteForByteIdentical()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        var codec = new AsterixCodec(
            new AsterixDecoder(registry, DecodeMode.Strict),
            new Asterix.Codec.Encode.AsterixEncoder(registry));

        byte[] result = codec.RoundTrip(PayloadFixtures.Cat062WithFspecRepetitive);
        result.Should().Equal(PayloadFixtures.Cat062WithFspecRepetitive);
    }
}
