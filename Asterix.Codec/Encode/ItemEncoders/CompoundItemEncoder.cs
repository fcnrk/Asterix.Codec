using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes a <see cref="CompoundDecodedItem"/> into <paramref name="writer"/> according to
/// <paramref name="definition"/>.
///
/// <para>
/// The inner FSPEC is always rebuilt from the subitems actually present in the decoded item
/// (never stored as raw bytes). Subitems are then written in <see cref="CompoundItemDefinition.Fspec"/>
/// order (UAP order), not dictionary order.
/// </para>
/// </summary>
internal static class CompoundItemEncoder
{
    internal static void Encode(
        BitWriter writer,
        CompoundDecodedItem item,
        CompoundItemDefinition definition,
        string itemPath)
    {
        var presentIds = new HashSet<string>(item.Subitems.Keys, StringComparer.Ordinal);

        FspecBuilder.WriteFspec(definition.Fspec, presentIds, writer);

        foreach (string subitemId in definition.Fspec)
        {
            if (!presentIds.Contains(subitemId))
                continue;

            if (!item.Subitems.TryGetValue(subitemId, out DecodedItem? subitem))
                continue; // should not happen — presentIds was built from Subitems.Keys, should I throw?

            if (!definition.Subitems.TryGetValue(subitemId, out ItemDefinition? subitemDef))
                throw new EncodeException($"{itemPath}.{subitemId}",
                    $"Subitem '{subitemId}' present in decoded item but not defined in compound schema");

            ItemEncoderDispatcher.Encode(writer, subitem, subitemDef, $"{itemPath}.{subitemId}");
        }
    }
}
