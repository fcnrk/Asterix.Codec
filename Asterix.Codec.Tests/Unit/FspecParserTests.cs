using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class FspecParserTests
{
    // ── ReadPresence ──────────────────────────────────────────────────────────

    [Fact]
    public void ReadPresence_SingleByte_NoFx_Returns7Flags()
    {
        // 0xA0 = 1010 0000 → bits 7..1 = [1,0,1,0,0,0,0], FX=0
        var reader = new BitReader([0xA0]);
        bool[] presence = FspecParser.ReadPresence(ref reader);

        presence.Should().HaveCount(7);
        presence[0].Should().BeTrue();   // bit 7
        presence[1].Should().BeFalse();  // bit 6
        presence[2].Should().BeTrue();   // bit 5
        presence[3].Should().BeFalse();  // bit 4
        presence[4].Should().BeFalse();  // bit 3
        presence[5].Should().BeFalse();  // bit 2
        presence[6].Should().BeFalse();  // bit 1
    }

    [Fact]
    public void ReadPresence_TwoBytesWithFx_Returns14Flags()
    {
        // Byte 0: 0x81 = 1000 0001 → bits [1,0,0,0,0,0,0], FX=1 → continue
        // Byte 1: 0x20 = 0010 0000 → bits [0,0,1,0,0,0,0], FX=0 → stop
        var reader = new BitReader([0x81, 0x20]);
        bool[] presence = FspecParser.ReadPresence(ref reader);

        presence.Should().HaveCount(14);
        presence[0].Should().BeTrue();   // I062_010
        presence[9].Should().BeTrue();   // I062_245 (pos 9 in 0-indexed 14-bit array)
    }

    [Fact]
    public void ReadPresence_AllBitsSet_FirstByte_AllPresent()
    {
        // 0xFE = 1111 1110 → all 7 presence bits set, FX=0
        var reader = new BitReader([0xFE]);
        bool[] presence = FspecParser.ReadPresence(ref reader);

        presence.Should().HaveCount(7);
        presence.Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public void ReadPresence_AllZero_NonePresent()
    {
        var reader = new BitReader([0x00]);
        bool[] presence = FspecParser.ReadPresence(ref reader);

        presence.Should().HaveCount(7);
        presence.Should().AllBeEquivalentTo(false);
    }

    [Fact]
    public void ReadPresence_ThreeChainedBytes_Returns21Flags()
    {
        // Bytes: 0x01 (FX=1), 0x01 (FX=1), 0xFE (FX=0)
        var reader = new BitReader([0x01, 0x01, 0xFE]);
        bool[] presence = FspecParser.ReadPresence(ref reader);

        presence.Should().HaveCount(21);
        // Third byte = all 7 presence bits set → flags 14..20 all true
        for (int i = 14; i < 21; i++)
            presence[i].Should().BeTrue($"presence[{i}] should be true");
    }

    [Fact]
    public void ReadPresence_AdvancesReaderCorrectly()
    {
        // After reading 2 FSPEC bytes, reader should be at byte offset 2
        var reader = new BitReader([0x01, 0x00, 0xAB]);
        FspecParser.ReadPresence(ref reader);
        reader.ByteOffset.Should().Be(2);
        reader.ReadBits(8).Should().Be(0xAB);
    }

    // ── GetPresentItemIds ─────────────────────────────────────────────────────

    [Fact]
    public void GetPresentItemIds_SomePresent_ReturnsOnlyPresent()
    {
        bool[] presence = [true, false, true, false, false, false, false];
        var uap = new[] { "I_A", "I_B", "I_C", "I_D", "I_E", "I_F", "I_G" };

        var present = FspecParser.GetPresentItemIds(presence, uap);

        present.Should().Equal("I_A", "I_C");
    }

    [Fact]
    public void GetPresentItemIds_NonePresent_ReturnsEmpty()
    {
        bool[] presence = [false, false, false];
        var uap = new[] { "X", "Y", "Z" };

        FspecParser.GetPresentItemIds(presence, uap).Should().BeEmpty();
    }

    [Fact]
    public void GetPresentItemIds_PresenceLongerThanUap_IgnoresExtras()
    {
        // Extra presence bits beyond UAP should be silently ignored
        bool[] presence = [true, true, true, true, true, true, true]; // 7 bits
        var uap = new[] { "A", "B" }; // only 2 items

        var present = FspecParser.GetPresentItemIds(presence, uap);
        present.Should().Equal("A", "B");
    }

    [Fact]
    public void GetPresentItemIds_PreservesUapOrder()
    {
        bool[] presence = [true, true, true];
        var uap = new[] { "Z", "M", "A" };

        var present = FspecParser.GetPresentItemIds(presence, uap);
        present.Should().Equal("Z", "M", "A"); // UAP order, not alphabetic
    }
}
