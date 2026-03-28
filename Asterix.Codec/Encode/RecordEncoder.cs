using Asterix.Codec.Binary;
using Asterix.Codec.Encode.ItemEncoders;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode;

/// <summary>
/// Encodes a single <see cref="DecodedRecord"/> to binary.
///
/// <para>
/// The FSPEC is always rebuilt from the item IDs present in <see cref="DecodedRecord.Items"/>
/// (never stored as raw bytes). Items are then written in UAP order (not dictionary order).
/// </para>
/// </summary>
internal static class RecordEncoder
{
    internal static void Encode(
        BitWriter writer,
        DecodedRecord record,
        AsterixCategorySchema schema,
        MessageDefinition message)
    {
        var presentIds = new HashSet<string>(record.Items.Keys, StringComparer.Ordinal);

        FspecBuilder.WriteFspec(message.Uap, presentIds, writer);

        foreach (string itemId in message.Uap)
        {
            if (!presentIds.Contains(itemId))
                continue;

            if (!record.Items.TryGetValue(itemId, out DecodedItem? item))
                continue; // built from Items.Keys — should not happen

            if (!schema.Items.TryGetValue(itemId, out ItemDefinition? itemDef))
                throw new EncodeException(itemId,
                    $"Item '{itemId}' present in record but not defined in " +
                    $"CAT{schema.Category:D3} schema");

            ItemEncoderDispatcher.Encode(writer, item, itemDef, itemId);
        }
    }
}
