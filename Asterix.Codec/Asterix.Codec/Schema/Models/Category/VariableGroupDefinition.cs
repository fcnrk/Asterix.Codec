namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Defines the named fields within one byte group of a <see cref="VariableItemDefinition"/>.
///
/// <para>
/// Each group occupies exactly one byte on the wire:
/// <code>
///   bit 7 … bit 1 : data bits  (7 usable bits, MSB-first)
///   bit 0          : FX         (0 = last group, 1 = more groups follow)
/// </code>
/// </para>
///
/// <para>
/// <see cref="Fields"/> describes the named fields within the 7 data bits.
/// <see cref="FieldDefinition.BitOffset"/> is 0-indexed from the MSB of the 7-bit
/// data space (0 = bit 7 of the wire byte, 6 = bit 1 of the wire byte). Spare bits
/// (unused positions between named fields, or trailing unused positions before FX)
/// are not represented in <see cref="Fields"/>; the decoder and encoder zero-pad them.
/// </para>
///
/// <para>
/// A group at position 0 in <see cref="VariableItemDefinition.Groups"/> is called
/// the <em>primary subfield</em>; groups at positions 1, 2, … are the first, second,
/// … <em>extension subfields</em>. The primary subfield is always present; each
/// extension subfield is present only when the preceding group's FX bit equals 1.
/// </para>
/// </summary>
public sealed class VariableGroupDefinition
{
    /// <summary>
    /// Named fields within the 7 data bits of this group, in declaration order.
    /// <see cref="FieldDefinition.BitOffset"/> values must be in 0..6.
    /// </summary>
    public IReadOnlyList<FieldDefinition> Fields { get; }

    public VariableGroupDefinition(IReadOnlyList<FieldDefinition> fields)
    {
        Fields = fields;
    }
}
