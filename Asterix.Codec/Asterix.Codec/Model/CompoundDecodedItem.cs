namespace Asterix.Codec.Model;

/// <summary>
/// A decoded ASTERIX compound item whose subitems were selected by an inner FSPEC.
/// Only present subitems appear in <see cref="Subitems"/>.
/// </summary>
public sealed class CompoundDecodedItem : DecodedItem
{
    /// <summary>
    /// Present subitems keyed by subitem ID (e.g. <c>"qx"</c>, <c>"adr"</c>).
    /// </summary>
    public IReadOnlyDictionary<string, DecodedItem> Subitems { get; }

    public CompoundDecodedItem(IReadOnlyDictionary<string, DecodedItem> subitems) =>
        Subitems = subitems;

    public bool TryGet(string subitemId, out DecodedItem? item) =>
        Subitems.TryGetValue(subitemId, out item);
}
