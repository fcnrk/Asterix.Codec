using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class ScaleFactorTests
{
    [Fact]
    public void Value_OneOverOneHundredTwentyEight_CorrectDouble()
    {
        var scale = new ScaleFactor(1, 128);
        scale.Value.Should().BeApproximately(1.0 / 128.0, precision: 1e-15);
    }

    [Fact]
    public void Value_OneEightyOverTwoGig_CorrectDouble()
    {
        var scale = new ScaleFactor(180, 2147483648);
        scale.Value.Should().BeApproximately(180.0 / 2147483648.0, precision: 1e-15);
    }

    [Fact]
    public void Value_ThreeSixtyOverSixtyFiveThousandFiveHundredThirtySix_CorrectDouble()
    {
        var scale = new ScaleFactor(360, 65536);
        scale.Value.Should().BeApproximately(360.0 / 65536.0, precision: 1e-15);
    }

    [Fact]
    public void Value_AppliedToRaw_GivesExpectedScaledValue()
    {
        // I062_070 time: raw=9600, scale=1/128, expected=75.0
        var scale = new ScaleFactor(1, 128);
        (9600 * scale.Value).Should().BeApproximately(75.0, precision: 1e-10);
    }

    [Fact]
    public void Value_AppliedToNegative_PreservesSign()
    {
        // I062_100 velocity: raw=-4 (as signed), scale=1/4, expected=-1.0 m/s
        var scale = new ScaleFactor(1, 4);
        (-4L * scale.Value).Should().BeApproximately(-1.0, precision: 1e-10);
    }

    [Fact]
    public void ScaleFactor_RecordStruct_EqualityByValue()
    {
        var a = new ScaleFactor(1, 128);
        var b = new ScaleFactor(1, 128);
        a.Should().Be(b);
    }
}
