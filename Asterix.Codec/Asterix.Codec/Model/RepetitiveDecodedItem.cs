namespace Asterix.Codec.Model;

/// <summary>
/// A decoded ASTERIX repetitive item: a sequence of identically structured elements.
/// </summary>
public sealed class RepetitiveDecodedItem : DecodedItem
{
    /// <summary>
    /// Decoded elements in order. Each element is decoded by the same item definition.
    /// </summary>
    public IReadOnlyList<DecodedItem> Elements { get; }

    public RepetitiveDecodedItem(IReadOnlyList<DecodedItem> elements) => Elements = elements;

    public int Count => Elements.Count;
}
