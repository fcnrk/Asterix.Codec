namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An SPF structure entry that reads a fixed-width scalar value.
///
/// <para>
/// Used for length fields, count fields, and any other plain numeric values
/// that appear before conditional or repetitive structures. The decoded value
/// is stored in <c>DecodeContext</c> under <see cref="SpfStructureEntry.Name"/>
/// so it can be referenced by downstream <see cref="SpfRepetitiveEntry.CountRef"/>
/// or <see cref="OptionalEntry.PresenceGroup"/> entries.
/// </para>
/// </summary>
public sealed class ScalarEntry : SpfStructureEntry
{
    public FieldType Type { get; }

    /// <summary>
    /// Bit width of this scalar. Must be a positive multiple of 8.
    /// </summary>
    public int Bits { get; }

    public ScalarEntry(string name, FieldType type, int bits) : base(name)
    {
        Type = type;
        Bits = bits;
    }
}
