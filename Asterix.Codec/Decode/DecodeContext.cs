namespace Asterix.Codec.Decode;

/// <summary>
/// Scoped name→value map used by <see cref="SpfDecoder"/> to resolve
/// forward-references in SPF field set structures.
///
/// <para>
/// A fresh <see cref="DecodeContext"/> is created for each SPF block decode.
/// As each entry in the SPF structure is decoded, its value is stored here.
/// Later entries (e.g. <c>count_ref</c>, <c>present_if</c>) look up values by name.
/// </para>
///
/// <para>
/// Key format for <c>dynamic_presence</c> entries: <c>"&lt;groupName&gt;.&lt;fieldName&gt;"</c>
/// (e.g. <c>"presence.f4"</c>), matching the dot-path used in <c>present_if</c>.
/// </para>
/// </summary>
public sealed class DecodeContext
{
    private readonly Dictionary<string, ulong> _values = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores a decoded scalar value under <paramref name="name"/>.
    /// </summary>
    public void Set(string name, ulong value) => _values[name] = value;

    /// <summary>
    /// Returns true and sets <paramref name="value"/> if <paramref name="name"/> exists.
    /// </summary>
    public bool TryGet(string name, out ulong value) =>
        _values.TryGetValue(name, out value);

    /// <summary>
    /// Returns the stored value for <paramref name="name"/>.
    /// Throws <see cref="InvalidOperationException"/> if the name was not previously set.
    /// This is a programming error: <c>SchemaValidator</c> guarantees all references are
    /// backward-only, so a missing key means a validator bug or corrupted schema.
    /// </summary>
    public ulong Get(string name) =>
        _values.TryGetValue(name, out var v)
            ? v
            : throw new InvalidOperationException(
                $"DecodeContext: '{name}' not found. This indicates a schema validation bug.");

    /// <summary>
    /// Returns true when the named presence flag is non-zero (field is present).
    /// </summary>
    public bool IsPresent(string presenceGroup, string presenceField) =>
        TryGet($"{presenceGroup}.{presenceField}", out ulong val) && val != 0;
}