using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes a <see cref="CompoundItemDefinition"/> from a <see cref="BitReader"/>.
///
/// <para>
/// A compound item has its own inner FSPEC (same FX-bit chaining as the record FSPEC)
/// that selects which subitems are present. The inner FSPEC maps to
/// <see cref="CompoundItemDefinition.Fspec"/> the same way the record FSPEC maps to the UAP.
/// </para>
/// </summary>
internal static class CompoundItemDecoder
{
    internal static CompoundDecodedItem Decode(
        ref BitReader reader,
        CompoundItemDefinition definition,
        string itemPath,
        DecodeMode mode)
    {
        // Read inner FSPEC — uses the same FX-chaining mechanism as the record FSPEC.
        bool[] presence;
        try
        {
            presence = FspecParser.ReadPresence(ref reader);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, itemPath,
                "Failed to read inner FSPEC for compound item", ex);
        }

        // Map presence bits → subitem IDs using definition.Fspec as the "UAP".
        IReadOnlyList<string> presentSubitemIds =
            FspecParser.GetPresentItemIds(presence, definition.Fspec);

        // Strict: presence bits beyond the defined fspec list are an error.
        if (mode == DecodeMode.Strict && presence.Length > definition.Fspec.Count)
        {
            for (int i = definition.Fspec.Count; i < presence.Length; i++)
            {
                if (presence[i])
                    throw new DecodeException(reader.ByteOffset, itemPath,
                        $"FSPEC bit {i} is set but compound item only defines {definition.Fspec.Count} subitems");
            }
        }

        var subitems = new Dictionary<string, DecodedItem>(presentSubitemIds.Count);

        foreach (string subitemId in presentSubitemIds)
        {
            if (!definition.Subitems.TryGetValue(subitemId, out ItemDefinition? subitemDef))
            {
                if (mode == DecodeMode.Strict)
                    throw new DecodeException(reader.ByteOffset, $"{itemPath}.{subitemId}",
                        $"Subitem '{subitemId}' present in FSPEC but not defined in compound item");
                continue; // lenient: skip unknown subitem (no bytes to skip — schema is unknown)
            }

            string subitemPath = $"{itemPath}.{subitemId}";
            subitems[subitemId] = ItemDecoderDispatcher.Decode(ref reader, subitemDef, subitemPath, mode);
        }

        return new CompoundDecodedItem(subitems);
    }
}