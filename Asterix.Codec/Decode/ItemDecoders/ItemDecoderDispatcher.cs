using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Central dispatch point for item decoding.
///
/// <para>
/// C# does not support <c>ref struct</c> parameters in interface methods, so polymorphic
/// dispatch via an <c>IItemDecoder</c> interface is not possible with <see cref="BitReader"/>.
/// This static class uses pattern matching on the concrete <see cref="ItemDefinition"/> type
/// instead — the JIT optimises this to an efficient type-check sequence with no boxing.
/// </para>
/// </summary>
internal static class ItemDecoderDispatcher
{
    /// <summary>
    /// Decodes one item using the appropriate decoder for <paramref name="definition"/>'s type.
    /// </summary>
    /// <param name="reader">Positioned at the start of the item's bytes.</param>
    /// <param name="definition">The runtime schema definition for this item.</param>
    /// <param name="itemPath">Dot-path for error context (e.g. <c>"I062_380.adr"</c>).</param>
    /// <param name="mode">Strict or lenient error handling.</param>
    internal static DecodedItem Decode(
        ref BitReader reader,
        ItemDefinition definition,
        string itemPath,
        DecodeMode mode)
    {
        return definition switch
        {
            FixedItemDefinition fixedItem => FixedItemDecoder.Decode(ref reader, fixedItem, itemPath, mode),
            CompoundItemDefinition compound => CompoundItemDecoder.Decode(ref reader, compound, itemPath, mode),
            RepetitiveItemDefinition rep => RepetitiveItemDecoder.Decode(ref reader, rep, itemPath, mode),
            VariableItemDefinition variable => VariableItemDecoder.Decode(ref reader, variable, itemPath, mode),
            StructuredExplicitItemDefinition seItem => StructuredExplicitItemDecoder.Decode(ref reader, seItem, itemPath, mode),
            FspecRepetitiveItemDefinition fspecRep => FspecRepetitiveItemDecoder.Decode(ref reader, fspecRep, itemPath, mode),
            ExplicitItemDefinition => ExplicitItemDecoder.Decode(ref reader, itemPath),

            _ => throw new DecodeException(reader.ByteOffset, itemPath,
                $"No decoder registered for item type '{definition.GetType().Name}'")
        };
    }
}