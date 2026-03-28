using Asterix.Codec.Binary;
using Asterix.Codec.Encode;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class FspecBuilderTests
{
    private static readonly string[] Uap =
        ["I_A", "I_B", "I_C", "I_D", "I_E", "I_F", "I_G",
         "I_H", "I_I", "I_J", "I_K", "I_L", "I_M", "I_N"];

    // ── Single-byte FSPEC (no FX needed) ──────────────────────────────────────

    [Fact]
    public void BuildFspec_FirstAndThirdItem_CorrectSingleByte()
    {
        // I_A (pos 0) + I_C (pos 2) present: bit7=1, bit5=1, FX=0 → 0xA0
        var present = new HashSet<string> { "I_A", "I_C" };
        FspecBuilder.BuildFspec(Uap[..7], present).Should().Equal(0xA0);
    }

    [Fact]
    public void BuildFspec_AllSevenPresent_AllBitsSet()
    {
        var present = new HashSet<string>(Uap[..7]);
        FspecBuilder.BuildFspec(Uap[..7], present).Should().Equal(0xFE);
    }

    [Fact]
    public void BuildFspec_NonePresent_ReturnsEmpty()
    {
        // No items present → no FSPEC bytes needed (FspecBuilder design: write nothing,
        // not a zero byte, for an empty record)
        FspecBuilder.BuildFspec(Uap[..7], new HashSet<string>())
            .Should().BeEmpty();
    }

    // ── Two-byte FSPEC (FX required) ──────────────────────────────────────────

    [Fact]
    public void BuildFspec_ItemInSecondByte_WritesTwoBytesWithFx()
    {
        // I_A (pos 0) + I_H (pos 7) — needs FX in byte 0
        var present = new HashSet<string> { "I_A", "I_H" };
        byte[] fspec = FspecBuilder.BuildFspec(Uap, present);

        fspec.Should().HaveCount(2);
        fspec[0].Should().Be(0x81); // bit7=1 (I_A), FX=1
        fspec[1].Should().Be(0x80); // bit7=1 (I_H), FX=0
    }

    // ── WriteFspec round-trip with FspecParser ────────────────────────────────

    [Fact]
    public void WriteFspec_ThenReadPresence_RoundTrip()
    {
        var present = new HashSet<string> { "I_B", "I_D", "I_K" };
        var writer = new BitWriter();
        FspecBuilder.WriteFspec(Uap, present, writer);

        var reader = new BitReader(writer.ToSpan());
        bool[] presence = Decode.FspecParser.ReadPresence(ref reader);
        var recovered = Decode.FspecParser.GetPresentItemIds(presence, Uap);

        recovered.Should().BeEquivalentTo(present);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildFspec_ItemNotInUap_Ignored()
    {
        // "UNKNOWN" is not in UAP — should not raise, just excluded
        var present = new HashSet<string> { "I_A", "UNKNOWN" };
        byte[] fspec = FspecBuilder.BuildFspec(Uap[..7], present);

        fspec.Should().Equal(0x80); // only I_A (bit7)
    }

    [Fact]
    public void BuildFspec_LastItemIn14UapEntry_CorrectSecondByte()
    {
        // I_N is at pos 13 → second byte, bit 1 (FX is bit 0)
        var present = new HashSet<string> { "I_N" };
        byte[] fspec = FspecBuilder.BuildFspec(Uap, present);

        fspec.Should().HaveCount(2);
        fspec[0].Should().Be(0x01); // FX=1 only
        fspec[1].Should().Be(0x02); // bit 1 = I_N
    }
}
