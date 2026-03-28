namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Represents a rational scale factor applied to a raw integer field value.
/// Precomputes <see cref="Value"/> = Numerator / Denominator to avoid repeated division at decoding time.
/// </summary>
public readonly record struct ScaleFactor(double Numerator, double Denominator)
{
    public double Value => Numerator / Denominator;

    /// <summary>
    /// Convenience factory for scalar scales (e.g. 0.25 → 0.25/1).
    /// </summary>
    public static ScaleFactor FromDouble(double value) => new(value, 1.0);
}
