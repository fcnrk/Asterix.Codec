using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes a <see cref="FixedItemDefinition"/> from a <see cref="BitReader"/>.
///
/// <para>
/// Consumes exactly <see cref="FixedItemDefinition.Length"/> bytes from the main
/// <paramref name="reader"/> as a zero-copy slice, then decodes each field from a
/// local <see cref="BitReader"/> over those bytes. This isolates the item's bits
/// from the record stream and allows the field loop to <see cref="BitReader.Skip"/>
/// spare bits by <see cref="FieldDefinition.BitOffset"/> without affecting the
/// outer reader's position.
/// </para>
///
/// <para>
/// The packet-absolute byte offset is captured before <c>ReadBytes</c> (while the
/// main reader is still positioned at the item start) and passed to
/// <see cref="FieldDecoder"/> as <c>baseByteOffset</c> so that decode errors report
/// the correct packet location even though the field is read from a local reader
/// whose <c>ByteOffset</c> is relative to the item start.
/// </para>
/// </summary>
internal static class FixedItemDecoder
{
    internal static FixedDecodedItem Decode(
        ref BitReader reader,
        FixedItemDefinition definition,
        string itemPath,
        DecodeMode mode)
    {
        // Capture the item's packet-absolute start offset BEFORE advancing the reader.
        int itemStartByte = reader.ByteOffset;

        ReadOnlySpan<byte> itemBytes;
        try
        {
            itemBytes = reader.ReadBytes(definition.Length);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(itemStartByte, itemPath,
                $"Not enough bytes for fixed item '{itemPath}' " +
                $"(need {definition.Length}, have {reader.RemainingBits / 8})", ex);
        }

        var localReader = new BitReader(itemBytes);
        var fields = new DecodedField[definition.Fields.Count];
        int currentBit = 0;

        for (int i = 0; i < definition.Fields.Count; i++)
        {
            FieldDefinition fieldDef = definition.Fields[i];
            string fieldPath = $"{itemPath}.{fieldDef.Name}";

            // Skip spare bits between the previous field boundary and this field's offset.
            if (fieldDef.BitOffset > currentBit)
                localReader.Skip(fieldDef.BitOffset - currentBit);

            // baseByteOffset = packet offset of the item's first byte.
            // localReader.ByteOffset = within-item offset at this field.
            // FieldDecoder adds them: error offset = itemStart + withinItemOffset.
            fields[i] = FieldDecoder.Decode(ref localReader, fieldDef, fieldPath, itemStartByte);
            currentBit = fieldDef.BitOffset + fieldDef.Bits;
        }

        return new FixedDecodedItem(fields);
    }
}
