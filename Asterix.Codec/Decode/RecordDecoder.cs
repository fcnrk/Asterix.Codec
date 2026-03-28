using Asterix.Codec.Binary;
using Asterix.Codec.Decode.ItemDecoders;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode;

/// <summary>
/// Decodes a single ASTERIX record from a <see cref="BitReader"/>.
///
/// <para>
/// A record begins with a variable-length FSPEC. The parser reads FSPEC bytes,
/// maps the presence bits to item IDs using the message's UAP, then dispatches
/// each present item to the appropriate <see cref="ItemDecoders.ItemDecoderDispatcher"/>.
/// </para>
///
/// <para>
/// The caller is responsible for slicing the record bytes to exactly the record's
/// bounds. This decoder reads until all present items are consumed; it does not
/// know the record's total byte length ahead of time.
/// </para>
/// </summary>
internal static class RecordDecoder
{
    internal static DecodedRecord Decode(
        ref BitReader reader,
        AsterixCategorySchema schema,
        MessageDefinition message,
        DecodeMode mode)
    {
        int recordStartByte = reader.ByteOffset;

        bool[] presence;
        try
        {
            presence = FspecParser.ReadPresence(ref reader);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, string.Empty,
                $"Failed to read FSPEC for CAT{schema.Category:D3} record", ex);
        }

        // Strict: presence bits that extend beyond the UAP are only allowed if all are zero.
        if (mode == DecodeMode.Strict && presence.Length > message.Uap.Count)
        {
            for (int i = message.Uap.Count; i < presence.Length; i++)
            {
                if (presence[i])
                    throw new DecodeException(recordStartByte, string.Empty,
                        $"FSPEC bit {i} is set but CAT{schema.Category:D3} UAP only defines " +
                        $"{message.Uap.Count} items");
            }
        }

        IReadOnlyList<string> presentItemIds = FspecParser.GetPresentItemIds(presence, message.Uap);

        var items = new Dictionary<string, DecodedItem>(presentItemIds.Count);

        foreach (string itemId in presentItemIds)
        {
            if (!schema.Items.TryGetValue(itemId, out ItemDefinition? itemDef))
            {
                if (mode == DecodeMode.Strict)
                    throw new DecodeException(reader.ByteOffset, itemId,
                        $"Item '{itemId}' present in FSPEC but not defined in " +
                        $"CAT{schema.Category:D3} schema");
                // Lenient: we cannot skip the item because we don't know its byte length.
                // This should never occur for a validated schema (SchemaValidator catches it).
                continue;
            }

            items[itemId] = ItemDecoderDispatcher.Decode(ref reader, itemDef, itemId, mode);
        }

        return new DecodedRecord(items);
    }

    /// <summary>
    /// Decodes a single ASTERIX record from a discriminated category (e.g. CAT253).
    ///
    /// <para>
    /// Two-phase algorithm:
    /// </para>
    /// <list type="number">
    ///   <item>Read raw FSPEC bytes (UAP-agnostic).</item>
    ///   <item>Decode the discriminator item (UAP position 0, always present, always fixed).</item>
    ///   <item>Extract the discriminator field value and match against <see cref="MessageDefinition.Discriminator"/>.</item>
    ///   <item>Map the full presence array using the selected message's UAP.</item>
    ///   <item>Decode remaining items; the discriminator item is already in the result.</item>
    /// </list>
    /// </summary>
    internal static DecodedRecord DecodeDiscriminated(
        ref BitReader reader,
        AsterixCategorySchema schema,
        DecodeMode mode)
    {
        var disc = schema.MessageDiscriminator!;

        // Phase 1: read raw FSPEC (UAP-agnostic)
        bool[] presence;
        try
        {
            presence = FspecParser.ReadPresence(ref reader);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, string.Empty,
                $"Failed to read FSPEC for CAT{schema.Category:D3} record", ex);
        }

        // Phase 2: discriminator item is always at UAP position 0 and must be present
        if (presence.Length == 0 || !presence[0])
            throw new DecodeException(reader.ByteOffset, disc.ItemId,
                $"Discriminator item '{disc.ItemId}' must always be present in CAT{schema.Category:D3}");

        var discItemDef = schema.Items[disc.ItemId]; // guaranteed FixedItemDefinition by SchemaValidator
        var discDecoded = (FixedDecodedItem)ItemDecoderDispatcher.Decode(ref reader, discItemDef, disc.ItemId, mode);

        // Phase 3: extract discriminator value and select message
        FixedDecodedItem? discFixed = discDecoded;
        DecodedField? discField = null;
        for (int i = 0; i < discFixed.Fields.Count; i++)
        {
            if (discFixed.Fields[i].Name == disc.FieldName)
            {
                discField = discFixed.Fields[i];
                break;
            }
        }

        if (discField is null)
            throw new DecodeException(reader.ByteOffset, disc.ItemId,
                $"Discriminator field '{disc.FieldName}' not found in decoded item '{disc.ItemId}'");

        string discValue = discField.RawValue.ToString();

        MessageDefinition? message = null;
        foreach (var m in schema.Messages)
        {
            if (m.Discriminator == discValue)
            {
                message = m;
                break;
            }
        }

        if (message is null)
        {
            if (mode == DecodeMode.Strict)
                throw new DecodeException(reader.ByteOffset, disc.ItemId,
                    $"No message definition for discriminator value '{discValue}' in CAT{schema.Category:D3}");
            message = schema.Messages[0];
        }

        // Phase 4: map full presence array using the selected message's UAP
        IReadOnlyList<string> allPresentIds = FspecParser.GetPresentItemIds(presence, message.Uap);

        var items = new Dictionary<string, DecodedItem>(allPresentIds.Count + 1);
        items[disc.ItemId] = discDecoded; // already decoded

        foreach (string itemId in allPresentIds)
        {
            if (itemId == disc.ItemId)
                continue; // already decoded

            if (!schema.Items.TryGetValue(itemId, out ItemDefinition? itemDef))
            {
                if (mode == DecodeMode.Strict)
                    throw new DecodeException(reader.ByteOffset, itemId,
                        $"Item '{itemId}' present in FSPEC but not defined in CAT{schema.Category:D3} schema");
                continue;
            }

            items[itemId] = ItemDecoderDispatcher.Decode(ref reader, itemDef, itemId, mode);
        }

        return new DecodedRecord(items);
    }
}