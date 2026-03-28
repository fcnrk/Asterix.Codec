namespace Asterix.Codec.Schema.Models;

/// <summary>
/// A fixed-length ASTERIX item containing one or more named bit fields.
///
/// <para>
/// <see cref="Length"/> defines the total byte size. The decoder reads exactly
/// <c>Length * 8</c> bits. Each <see cref="FieldDefinition"/> in <see cref="Fields"/>
/// carries a pre-resolved <see cref="FieldDefinition.BitOffset"/> and
/// <see cref="FieldDefinition.Bits"/> so the decoder can extract each field
/// without any offset arithmetic at runtime.
/// </para>
/// </summary>
public sealed class FixedItemDefinition : ItemDefinition
{
    /// <summary>
    /// Total byte length of this item.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Ordered list of fields within this item.
    /// <see cref="FieldDefinition.BitOffset"/> values are pre-resolved and non-overlapping.
    /// </summary>
    public IReadOnlyList<FieldDefinition> Fields { get; }

    public FixedItemDefinition(int length, IReadOnlyList<FieldDefinition> fields)
    {
        Length = length;
        Fields = fields;
    }
}
