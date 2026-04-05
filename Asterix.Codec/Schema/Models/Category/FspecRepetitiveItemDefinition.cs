namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An ASTERIX item whose elements are repeated N times, where N is the number
/// of set data bits in an FSPEC prefix (FX-bit extended, same mechanism as
/// <see cref="CompoundItemDefinition"/>).
///
/// <para>
/// Unlike <see cref="RepetitiveItemDefinition"/>, the count is not written
/// explicitly on the wire — it is derived by counting the set bits in the FSPEC.
/// Unlike <see cref="CompoundItemDefinition"/>, all elements have the same structure.
/// </para>
/// </summary>
public sealed class FspecRepetitiveItemDefinition : ItemDefinition
{
    /// <summary>
    /// Structure decoded for every set FSPEC bit.
    /// </summary>
    public ItemDefinition Element { get; }

    public FspecRepetitiveItemDefinition(ItemDefinition element)
        => Element = element;
}
