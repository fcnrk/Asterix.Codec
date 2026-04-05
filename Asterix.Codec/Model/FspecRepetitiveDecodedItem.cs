namespace Asterix.Codec.Model;

/// <summary>
/// A decoded ASTERIX fspec-repetitive item: a sequence of identically structured
/// elements whose count was determined by the number of set bits in an inner FSPEC.
/// </summary>
public sealed class FspecRepetitiveDecodedItem : DecodedItem
{
    /// <summary>
    /// Decoded elements in FSPEC bit order.
    /// </summary>
    public IReadOnlyList<DecodedItem> Elements { get; }

    public FspecRepetitiveDecodedItem(IReadOnlyList<DecodedItem> elements)
        => Elements = elements;

    public int Count => Elements.Count;
}
