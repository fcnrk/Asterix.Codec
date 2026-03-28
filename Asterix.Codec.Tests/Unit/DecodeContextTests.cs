using Asterix.Codec.Decode;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class DecodeContextTests
{
    [Fact]
    public void Set_ThenGet_ReturnsValue()
    {
        var ctx = new DecodeContext();
        ctx.Set("count", 42UL);
        ctx.Get("count").Should().Be(42UL);
    }

    [Fact]
    public void TryGet_ExistingKey_ReturnsTrueAndValue()
    {
        var ctx = new DecodeContext();
        ctx.Set("x", 99UL);
        ctx.TryGet("x", out ulong value).Should().BeTrue();
        value.Should().Be(99UL);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var ctx = new DecodeContext();
        ctx.TryGet("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Get_MissingKey_ThrowsInvalidOperation()
    {
        var ctx = new DecodeContext();
        ctx.Invoking(c => c.Get("missing"))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        var ctx = new DecodeContext();
        ctx.Set("x", 1UL);
        ctx.Set("x", 2UL);
        ctx.Get("x").Should().Be(2UL);
    }

    [Fact]
    public void IsPresent_NonZeroValue_ReturnsTrue()
    {
        var ctx = new DecodeContext();
        ctx.Set("presence.f4", 1UL);
        ctx.IsPresent("presence", "f4").Should().BeTrue();
    }

    [Fact]
    public void IsPresent_ZeroValue_ReturnsFalse()
    {
        var ctx = new DecodeContext();
        ctx.Set("presence.f4", 0UL);
        ctx.IsPresent("presence", "f4").Should().BeFalse();
    }

    [Fact]
    public void IsPresent_KeyNotSet_ReturnsFalse()
    {
        var ctx = new DecodeContext();
        ctx.IsPresent("presence", "f4").Should().BeFalse();
    }

    [Fact]
    public void IsPresent_LargeNonZeroValue_ReturnsTrue()
    {
        var ctx = new DecodeContext();
        ctx.Set("presence.f7", 0xFF00FF00UL);
        ctx.IsPresent("presence", "f7").Should().BeTrue();
    }
}
