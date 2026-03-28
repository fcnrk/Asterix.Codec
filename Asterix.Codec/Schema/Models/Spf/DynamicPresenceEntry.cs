namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An SPF structure entry that reads one presence flag per controlled field.
///
/// <para>
/// For each name in <see cref="Fields"/>, the decoder reads <see cref="BitWidth"/> bits
/// as an unsigned integer. A zero value means the field is absent; non-zero means present.
/// The results are stored in <c>DecodeContext</c> as <c>"&lt;Name&gt;.&lt;fieldName&gt;"</c>
/// (e.g. <c>"presence.f4"</c>) for use by downstream <see cref="OptionalEntry"/> entries.
/// </para>
///
/// <para>
/// <see cref="BitWidth"/> is the granularity of each flag, typically 8 (one byte per flag).
/// </para>
/// </summary>
public sealed class DynamicPresenceEntry : SpfStructureEntry
{
    /// <summary>
    /// Number of bits per presence flag. Typically 8.
    /// </summary>
    public int BitWidth { get; }

    /// <summary>
    /// Ordered list of field names whose presence this entry controls.
    /// The decoder reads one <see cref="BitWidth"/>-wide value per entry, in order.
    /// </summary>
    public IReadOnlyList<string> Fields { get; }

    public DynamicPresenceEntry(string name, int bitWidth, IReadOnlyList<string> fields) : base(name)
    {
        BitWidth = bitWidth;
        Fields = fields;
    }
}