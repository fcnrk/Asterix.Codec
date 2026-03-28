using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Schema;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Negative;

/// <summary>
/// Verifies that strict mode throws <see cref="DecodeException"/> with useful context
/// on all well-defined error conditions.
/// </summary>
public class StrictModeTests
{
    private static AsterixDecoder StrictDecoder()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        return new AsterixDecoder(registry, DecodeMode.Strict);
    }

    // ── Packet-level errors ───────────────────────────────────────────────────

    [Fact]
    public void Decode_DataTooShort_ThrowsDecodeException()
    {
        StrictDecoder().Invoking(d => d.Decode([0x3E, 0x00]))
            .Should().Throw<DecodeException>();
    }

    [Fact]
    public void Decode_LenExceedsData_ThrowsDecodeException()
    {
        StrictDecoder().Invoking(d => d.Decode(PayloadFixtures.LenExceedsData))
            .Should().Throw<DecodeException>();
    }

    [Fact]
    public void Decode_LenBelowMinimum_ThrowsDecodeException()
    {
        // LEN=2 is below minimum of 3
        StrictDecoder().Invoking(d => d.Decode([0x3E, 0x00, 0x02, 0x00]))
            .Should().Throw<DecodeException>();
    }

    [Fact]
    public void Decode_UnknownCategory_ThrowsDecodeException()
    {
        // Empty registry — CAT001 has no schema
        var registry = new SchemaRegistry();
        registry.Freeze();
        var decoder = new AsterixDecoder(registry, DecodeMode.Strict);

        // CAT=1, LEN=3 (empty record section)
        decoder.Invoking(d => d.Decode([0x01, 0x00, 0x03]))
            .Should().Throw<DecodeException>()
            .WithMessage("*CAT001*");
    }

    // ── SPF length boundary violations ────────────────────────────────────────

    [Fact]
    public void Decode_SpfBlockLengthTooShort_StrictMode_ThrowsDecodeException()
    {
        // Build an SPF block that declares length=5 but needs more bytes
        // length(2)=5, f1RecordCount(1)=2 → f1 would need 4 bytes but block only has 2 more
        byte[] block =
        [
            0x00, 0x05,    // length = 5 (too short for 2 F1 elements)
            0x02,          // f1RecordCount = 2
            0x0A, 0x0B,    // only 2 bytes remain but 4 needed
        ];

        Assert.Throws<DecodeException>(() => DecodeSPF(block, DecodeMode.Strict));
    }

    [Fact]
    public void Decode_SpfBlockLengthOverread_StrictMode_ThrowsDecodeException()
    {
        // Valid SPF content but length field declares fewer bytes than consumed
        byte[] tampered = [..PayloadFixtures.SpfCustom062Block];
        tampered[0] = 0x00;
        tampered[1] = 0x0A; // claim only 10 bytes

        Assert.Throws<DecodeException>(() => DecodeSPF(tampered, DecodeMode.Strict));
    }

    [Fact]
    public void Decode_SpfBlockLengthOverread_LenientMode_ClampsToLength()
    {
        byte[] tampered = [..PayloadFixtures.SpfCustom062Block];
        tampered[0] = 0x00;
        tampered[1] = 0x0A; // length=10

        // Lenient mode must not throw — it clamps to declared length
        var item = DecodeSPF(tampered, DecodeMode.Lenient);
        item.Should().NotBeNull();
    }

    // Ref struct can't be captured in lambdas, so extract SPF decode into a helper.
    private static Asterix.Codec.Model.SpfDecodedItem DecodeSPF(byte[] data, DecodeMode mode)
    {
        var definition = SchemaFixtures.SpfCustom062();
        var reader = new BitReader(data);
        return SpfDecoder.Decode(ref reader, definition, mode);
    }

    // ── FSPEC violations ──────────────────────────────────────────────────────

    [Fact]
    public void Decode_FspecBitSetBeyondUap_StrictMode_ThrowsDecodeException()
    {
        // FSPEC has bits set in positions 13+, which the 12-item UAP doesn't define
        // Two FSPEC bytes: byte0=0x01 (FX=1), byte1=0x02 (bit1 = UAP pos 13)
        byte[] packet =
        [
            0x3E, 0x00, 0x08, // header
            0x01, 0x02,       // FSPEC: FX=1 in byte0, bit1 set in byte1 → UAP pos 13
            0x00, 0x00, 0x00  // padding
        ];

        StrictDecoder().Invoking(d => d.Decode(packet))
            .Should().Throw<DecodeException>();
    }

    // ── DecodeException properties ────────────────────────────────────────────

    [Fact]
    public void DecodeException_ContainsByteOffset()
    {
        StrictDecoder()
            .Invoking(d => d.Decode(PayloadFixtures.LenExceedsData))
            .Should().Throw<DecodeException>()
            .Which.ByteOffset.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void DecodeException_ContainsMessage()
    {
        StrictDecoder()
            .Invoking(d => d.Decode([0x3E, 0x00, 0x02, 0x00]))
            .Should().Throw<DecodeException>()
            .WithMessage("*2*"); // message should mention the invalid LEN value
    }
}
