namespace Asterix.Codec.Model;

/// <summary>
/// A decoded variable-length ASTERIX item produced by <c>VariableItemDecoder</c>.
///
/// <para>
/// <see cref="Groups"/> holds the decoded groups in wire order.
/// <c>Groups[0]</c> is the primary subfield; <c>Groups[i]</c> for i ≥ 1 are extension
/// subfields, present only when the preceding group's FX bit was 1.
/// </para>
///
/// <para>
/// <see cref="GetField"/> provides a flat lookup across all groups, matching the common
/// access pattern where the caller knows the field name but not which group it is in.
/// </para>
/// </summary>
public sealed class VariableDecodedItem : DecodedItem
{
    /// <summary>
    /// Decoded groups in wire order. At least one group is always present (the primary
    /// subfield). Extension subfields follow at indices 1, 2, …
    /// </summary>
    public IReadOnlyList<IReadOnlyList<DecodedField>> Groups { get; }

    public VariableDecodedItem(IReadOnlyList<IReadOnlyList<DecodedField>> groups)
    {
        Groups = groups;
    }

    /// <summary>
    /// Returns the first <see cref="DecodedField"/> with <paramref name="name"/>
    /// across all groups, searching in wire order. Returns <c>null</c> if not found.
    /// </summary>
    public DecodedField? GetField(string name)
    {
        foreach (var group in Groups)
            foreach (var field in group)
                if (field.Name == name) return field;
        return null;
    }
}
