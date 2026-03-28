namespace Asterix.Codec.Model;

/// <summary>
/// A decoded SPF (Supplementary Field Package) item produced by <c>SpfDecoder</c>.
///
/// <para>
/// <see cref="Fields"/> maps each SPF structure entry name to its decoded value.
/// The runtime type of each value depends on the entry type in the schema:
/// </para>
///
/// <list type="table">
///   <listheader><term>Entry type</term><description>Value type in Fields</description></listheader>
///   <item><term>ScalarEntry</term>
///         <description><see cref="ulong"/> — raw bits (uint) or two's-complement bits (int)</description></item>
///   <item><term>SpfRepetitiveEntry</term>
///         <description><see cref="IReadOnlyList{SpfGroupValue}"/> — one element per repetition</description></item>
///   <item><term>DynamicPresenceEntry</term>
///         <description><see cref="IReadOnlyDictionary{String,UInt64}"/> — field name → flag value</description></item>
///   <item><term>OptionalEntry (present)</term>
///         <description><see cref="DecodedField"/> — decoded field value</description></item>
///   <item><term>OptionalEntry (absent)</term>
///         <description><c>null</c></description></item>
/// </list>
/// </summary>
public sealed class SpfDecodedItem : DecodedItem
{
    public IReadOnlyDictionary<string, object?> Fields { get; }

    public SpfDecodedItem(IReadOnlyDictionary<string, object?> fields) => Fields = fields;

    /// <summary>
    /// Returns the scalar value for <paramref name="name"/>, or null.
    /// </summary>
    public ulong? GetScalar(string name) =>
        Fields.TryGetValue(name, out var v) && v is ulong u ? u : null;

    /// <summary>
    /// Returns the repetitive elements for <paramref name="name"/>, or null.
    /// </summary>
    public IReadOnlyList<SpfGroupValue>? GetRepetitive(string name) =>
        Fields.TryGetValue(name, out var v) ? v as IReadOnlyList<SpfGroupValue> : null;

    /// <summary>
    /// Returns the presence flags for <paramref name="name"/>, or null.
    /// </summary>
    public IReadOnlyDictionary<string, ulong>? GetPresenceFlags(string name) =>
        Fields.TryGetValue(name, out var v) ? v as IReadOnlyDictionary<string, ulong> : null;

    /// <summary>
    /// Returns the optional field for <paramref name="name"/>, or null if absent.
    /// </summary>
    public DecodedField? GetOptional(string name) =>
        Fields.TryGetValue(name, out var v) ? v as DecodedField : null;
}
