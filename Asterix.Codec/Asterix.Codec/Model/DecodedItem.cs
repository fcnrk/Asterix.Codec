namespace Asterix.Codec.Model;

/// <summary>
/// Base class for all decoded ASTERIX data items.
///
/// <para>
/// Decoders dispatch on the concrete type — no string-keyed type checks at runtime.
/// Encode-side counterparts mirror the same hierarchy.
/// </para>
///
/// Concrete subtypes:
/// <list type="bullet">
///   <item><see cref="FixedDecodedItem"/> — fixed-length item with named bit fields</item>
///   <item><see cref="CompoundDecodedItem"/> — compound item with FSPEC-selected subitems</item>
///   <item><see cref="RepetitiveDecodedItem"/> — N repetitions of a fixed element</item>
///   <item><see cref="SpfDecodedItem"/> — SPF field set with dynamic presence</item>
/// </list>
/// </summary>
public abstract class DecodedItem { }
