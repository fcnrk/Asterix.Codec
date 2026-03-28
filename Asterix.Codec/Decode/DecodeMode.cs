namespace Asterix.Codec.Decode;

/// <summary>
/// Controls how the decoder handles anomalies.
/// </summary>
public enum DecodeMode
{
    /// <summary>
    /// Any schema violation, unknown item, or structural anomaly throws a
    /// <see cref="Exceptions.DecodeException"/> immediately.
    /// Recommended for development, testing, and trusted data sources.
    /// </summary>
    Strict,

    /// <summary>
    /// Unknown items and surplus FSPEC bits are silently skipped; decoding continues.
    /// Malformed data that cannot be recovered from still throws.
    /// Recommended for live feeds where forward-compatibility is required.
    /// </summary>
    Lenient
}
