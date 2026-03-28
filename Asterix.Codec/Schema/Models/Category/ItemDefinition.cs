namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Base class for all ASTERIX data item definitions.
///
/// <para>
/// The YAML <c>type</c> discriminator is resolved once at load time into the correct
/// subclass. Decoders dispatch via <c>switch</c> on the concrete type — no string comparisons
/// at runtime.
/// </para>
///
/// Concrete subtypes:
/// <list type="bullet">
///   <item><see cref="FixedItemDefinition"/> — fixed-length bit field container</item>
///   <item><see cref="CompoundItemDefinition"/> — FSPEC-controlled compound item</item>
///   <item><see cref="RepetitiveItemDefinition"/> — count-prefixed repetitive item</item>
///   <item><see cref="VariableItemDefinition"/> — FX-bit chained variable-length item</item>
///   <item><see cref="ExplicitItemDefinition"/> — length-prefixed opaque byte block (RE/SP)</item>
/// </list>
/// </summary>
public abstract class ItemDefinition { }
