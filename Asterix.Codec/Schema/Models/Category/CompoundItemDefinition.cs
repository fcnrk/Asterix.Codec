namespace Asterix.Codec.Schema.Models;

/// <summary>
/// A compound ASTERIX item whose presence of subitems is controlled by an inner FSPEC.
///
/// <para>
/// <see cref="Fspec"/> defines the ordered mapping from FSPEC bit positions to subitem names.
/// Index 0 corresponds to the first data bit of the first FSPEC byte (bit 7), index 6 to
/// bit 1 (bit 0 is the FX extension bit), index 7 to the second FSPEC byte's bit 7, and so on.
/// </para>
///
/// <para>
/// At decode time, the decoder reads FSPEC bytes (FX-chained), resolves which subitems
/// are present using <see cref="Fspec"/>, then decodes each present subitem by looking it
/// up in <see cref="Subitems"/>.
/// </para>
/// </summary>
public sealed class CompoundItemDefinition : ItemDefinition
{
    /// <summary>
    /// Ordered subitem names, one per FSPEC data bit position.
    /// </summary>
    public IReadOnlyList<string> Fspec { get; }

    /// <summary>
    /// All subitems keyed by name. Only those flagged present in FSPEC are decoded.
    /// </summary>
    public IReadOnlyDictionary<string, ItemDefinition> Subitems { get; }

    public CompoundItemDefinition(
        IReadOnlyList<string> fspec,
        IReadOnlyDictionary<string, ItemDefinition> subitems)
    {
        Fspec = fspec;
        Subitems = subitems;
    }
}
