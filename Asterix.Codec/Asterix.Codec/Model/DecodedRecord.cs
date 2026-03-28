namespace Asterix.Codec.Model;

/// <summary>
/// One decoded ASTERIX record: a set of data items selected by the record's FSPEC.
/// Only items that were present in the record appear in <see cref="Items"/>.
/// </summary>
public sealed class DecodedRecord
{
    /// <summary>
    /// Present data items keyed by item ID (e.g. <c>"I062_010"</c>).
    /// </summary>
    public IReadOnlyDictionary<string, DecodedItem> Items { get; }

    public DecodedRecord(IReadOnlyDictionary<string, DecodedItem> items) => Items = items;

    public bool TryGet(string itemId, out DecodedItem? item) =>
        Items.TryGetValue(itemId, out item);
}
