namespace Asterix.Codec.Schema.Models;

/// <summary>
/// A variable-length ASTERIX data item whose byte count is determined at decode time
/// by FX-bit chaining.
///
/// <para>
/// Wire format — each byte:
/// <code>
///   [B7][B6][B5][B4][B3][B2][B1][FX]
/// </code>
/// The decoder reads bytes while FX = 1 and stops when FX = 0.
/// </para>
///
/// <para>
/// <see cref="Groups"/> defines the field layout for each possible group position.
/// <c>Groups[0]</c> is the primary subfield (always present).
/// <c>Groups[i]</c> for i ≥ 1 is the i-th extension subfield (present only when
/// FX = 1 in the preceding group's byte).
/// </para>
///
/// <para>
/// If the wire data contains more groups than <see cref="Groups"/> defines, the
/// behaviour depends on decode mode:
/// </para>
/// <list type="bullet">
///   <item>Strict — throws <see cref="Exceptions.DecodeException"/>.</item>
///   <item>Lenient — extra groups are consumed and ignored (raw bytes discarded).</item>
/// </list>
/// </summary>
public sealed class VariableItemDefinition : ItemDefinition
{
    /// <summary>
    /// Ordered group definitions; index 0 = primary subfield.
    /// Must contain at least one entry (validated by <c>SchemaValidator</c>).
    /// </summary>
    public IReadOnlyList<VariableGroupDefinition> Groups { get; }

    public VariableItemDefinition(IReadOnlyList<VariableGroupDefinition> groups)
    {
        Groups = groups;
    }
}
