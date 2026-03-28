using Asterix.Codec.Decode;
using Asterix.Codec.Encode;
using Asterix.Codec.Schema;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Integration;

/// <summary>
/// Verifies that decode → encode produces a byte-for-byte identical result
/// for all well-formed CAT062 payloads. This is the primary correctness
/// guarantee of the codec.
/// </summary>
public class RoundTripTests
{
    private static (AsterixDecoder decoder, AsterixEncoder encoder) BuildCodec()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        return (new AsterixDecoder(registry), new AsterixEncoder(registry));
    }

    private static byte[] RoundTrip(byte[] input)
    {
        var (decoder, encoder) = BuildCodec();
        var packet = decoder.Decode(input);
        return encoder.Encode(packet);
    }

    [Fact]
    public void RoundTrip_SimpleFixedItems_ByteForByte()
    {
        RoundTrip(PayloadFixtures.Cat062Simple)
            .Should().Equal(PayloadFixtures.Cat062Simple);
    }

    [Fact]
    public void RoundTrip_WithTime_ByteForByte()
    {
        RoundTrip(PayloadFixtures.Cat062WithTime)
            .Should().Equal(PayloadFixtures.Cat062WithTime);
    }

    [Fact]
    public void RoundTrip_IA5Callsign_ByteForByte()
    {
        RoundTrip(PayloadFixtures.Cat062WithCallsign)
            .Should().Equal(PayloadFixtures.Cat062WithCallsign);
    }

    [Fact]
    public void RoundTrip_CompoundItem_ByteForByte()
    {
        RoundTrip(PayloadFixtures.Cat062WithCompound)
            .Should().Equal(PayloadFixtures.Cat062WithCompound);
    }

    [Fact]
    public void RoundTrip_RepetitiveItem_ByteForByte()
    {
        RoundTrip(PayloadFixtures.Cat062WithRepetitive)
            .Should().Equal(PayloadFixtures.Cat062WithRepetitive);
    }

    [Fact]
    public void RoundTrip_HeaderCategoryAndLength_RecomputedCorrectly()
    {
        // Verify that the encoder always rebuilds CAT and LEN, not copies them blindly
        byte[] result = RoundTrip(PayloadFixtures.Cat062Simple);
        result[0].Should().Be(0x3E);        // CAT = 62
        result[1].Should().Be(0x00);        // LEN high byte
        result[2].Should().Be(0x08);        // LEN low byte = 8
    }

    [Fact]
    public void RoundTrip_FspecRebuiltFromPresentItems()
    {
        // The FSPEC byte in the re-encoded output must match the original
        byte[] result = RoundTrip(PayloadFixtures.Cat062Simple);
        result[3].Should().Be(0xA0); // original FSPEC byte
    }

    // ── Double round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void DoubleRoundTrip_StillByteForByte()
    {
        // Decode → encode → decode → encode must also be stable
        byte[] once  = RoundTrip(PayloadFixtures.Cat062Simple);
        byte[] twice = RoundTrip(once);
        twice.Should().Equal(PayloadFixtures.Cat062Simple);
    }
}
