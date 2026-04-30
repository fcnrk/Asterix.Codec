namespace Asterix.Codec.Model;

/// <summary>
/// A decoded SPF optional repetitive item from an <c>OptionalRepetitiveEntry</c>,
/// present only when its presence flag was set.
///
/// <para>
/// This combines the repetition count and all decoded elements. The count is always a uint8
/// and is recorded for round-trip encoding accuracy and for diagnostics.
/// </para>
///
/// <para>Example: for the <c>f_optional_rep</c> optional repetitive entry, if the presence
/// flag is set and the inline count is 3, this instance holds count=3 and a list of 3
/// <see cref="SpfGroupValue"/> elements.</para>
/// </summary>
public sealed class SpfOptionalRepetitiveValue
{
    /// <summary>
    /// The repetition count read inline from the data stream (uint8).
    /// </summary>
    public byte Count { get; }

    /// <summary>
    /// Decoded elements in order, one per repetition.
    /// </summary>
    public IReadOnlyList<SpfGroupValue> Elements { get; }

    public SpfOptionalRepetitiveValue(byte count, IReadOnlyList<SpfGroupValue> elements)
    {
        Count = count;
        Elements = elements;
    }
}
