namespace Asterix.Codec.Model;

/// <summary>
/// Decoded form of a structured-explicit application data item produced by <c>StructuredExplicitItemDecoder</c>.
///
/// <para>
/// <see cref="Items"/> maps each inner-item Id (as defined in
/// <c>StructuredExplicitContentEntry.Id</c>) to its decoded value. The inner items may be of
/// any <see cref="DecodedItem"/> subtype: <see cref="FixedDecodedItem"/>,
/// <see cref="VariableDecodedItem"/>, <see cref="RepetitiveDecodedItem"/>,
/// <see cref="CompoundDecodedItem"/>, etc.
/// </para>
/// </summary>
public sealed class StructuredExplicitDecodedItem : DecodedItem
{
    /// <summary>
    /// Inner items keyed by their Id from the structured-explicit content schema.
    /// </summary>
    public IReadOnlyDictionary<string, DecodedItem> Items { get; }

    public StructuredExplicitDecodedItem(IReadOnlyDictionary<string, DecodedItem> items)
    {
        Items = items;
    }
}
