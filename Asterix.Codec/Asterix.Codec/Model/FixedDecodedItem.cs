namespace Asterix.Codec.Model;

/// <summary>
/// A decoded fixed-length ASTERIX item containing one or more named bit fields.
/// </summary>
public sealed class FixedDecodedItem : DecodedItem
{
    /// <summary>
    /// Fields in declaration order (matches the order in <see cref="Schema.Models.FixedItemDefinition.Fields"/>).
    /// </summary>
    public IReadOnlyList<DecodedField> Fields { get; }

    public FixedDecodedItem(IReadOnlyList<DecodedField> fields) => Fields = fields;

    /// <summary>
    /// Returns the first field with the given name, or null.
    /// </summary>
    public DecodedField? GetField(string name)
    {
        foreach (var f in Fields)
            if (f.Name == name) return f;
        return null;
    }
}
