using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes a <see cref="VariableItemDefinition"/> from a <see cref="BitReader"/>.
///
/// <para>
/// Wire format per byte:
/// <code>
///   [B7][B6][B5][B4][B3][B2][B1][FX]
///    ↑                           ↑
///    MSB (first bit read)        0 = last group, 1 = more groups follow
/// </code>
/// </para>
///
/// <para>
/// For each byte, the decoder:
/// </para>
/// <list type="number">
///   <item>Reads 1 byte (8 bits) from <paramref name="reader"/>.</item>
///   <item>Masks out the FX bit: <c>dataByte = wireValue &amp; 0xFE</c>.</item>
///   <item>Creates a 1-byte local <see cref="BitReader"/> over <c>dataByte</c>.</item>
///   <item>Decodes the group's fields from the local reader using
///         <see cref="FieldDecoder"/> — the same field-layout logic as
///         <see cref="FixedItemDecoder"/>.</item>
///   <item>Checks FX: <c>wireValue &amp; 0x01</c>. If 1, reads the next byte.</item>
/// </list>
///
/// <para>
/// If more groups arrive than the schema defines, the behaviour depends on
/// <paramref name="mode"/>: strict throws; lenient discards the extra bytes.
/// </para>
/// </summary>
internal static class VariableItemDecoder
{
    internal static VariableDecodedItem Decode(
        ref BitReader reader,
        VariableItemDefinition definition,
        string itemPath,
        DecodeMode mode)
    {
        var groups = new List<IReadOnlyList<DecodedField>>();
        int groupIndex = 0;

        while (true)
        {
            int groupStartByte = reader.ByteOffset;

            // Read the full wire byte.
            ulong wireValue;
            try
            {
                wireValue = reader.ReadBits(8);
            }
            catch (InvalidOperationException ex)
            {
                throw new DecodeException(reader.ByteOffset, itemPath,
                    $"Truncated variable item at group {groupIndex}: ran out of data", ex);
            }

            bool hasMore = (wireValue & 0x01) != 0;

            // The 7 data bits sit at bits 7..1 of the wire byte.
            // Keeping bit 0 as zero preserves the MSB positions when feeding a local reader.
            byte dataByte = (byte)(wireValue & 0xFE);

            if (groupIndex < definition.Groups.Count)
            {
                var groupDef = definition.Groups[groupIndex];
                var fields = DecodeGroupFields(dataByte, groupDef, $"{itemPath}[{groupIndex}]", groupStartByte);
                groups.Add(fields);
            }
            else
            {
                // No schema definition for this group position.
                if (mode == DecodeMode.Strict)
                    throw new DecodeException(groupStartByte, itemPath,
                        $"Variable item '{itemPath}' has more groups than the schema defines " +
                        $"({definition.Groups.Count}); group index {groupIndex} has no definition");
                // Lenient: byte was already consumed — discard silently.
            }

            groupIndex++;

            if (!hasMore)
                break;
        }

        return new VariableDecodedItem(groups);
    }

    /// <summary>
    /// Decodes the named fields from a single group's data byte.
    ///
    /// <para>
    /// <paramref name="dataByte"/> is the wire byte with FX zeroed out:
    /// <c>[B7][B6][B5][B4][B3][B2][B1][0]</c>.
    /// A local <see cref="BitReader"/> is created over this byte so that
    /// <see cref="FieldDecoder"/> can use <see cref="Schema.Models.FieldDefinition.BitOffset"/>
    /// directly (BitOffset 0 = bit 7 of the data byte = MSB).
    /// </para>
    /// </summary>
    private static IReadOnlyList<DecodedField> DecodeGroupFields(
        byte dataByte,
        VariableGroupDefinition groupDef,
        string groupPath,
        int baseByteOffset)
    {
        // Stack-allocate a 1-byte buffer so the local reader can be formed without heap allocation.
        Span<byte> buf = stackalloc byte[1];
        buf[0] = dataByte;

        var localReader = new BitReader(buf);
        var fields = new DecodedField[groupDef.Fields.Count];
        int currentBit = 0;

        for (int i = 0; i < groupDef.Fields.Count; i++)
        {
            var fieldDef = groupDef.Fields[i];

            if (fieldDef.BitOffset > currentBit)
                localReader.Skip(fieldDef.BitOffset - currentBit);

            // baseByteOffset is the packet-absolute position of the group's wire byte.
            // localReader.ByteOffset is 0 for all fields (single-byte group).
            fields[i] = FieldDecoder.Decode(ref localReader, fieldDef,
                $"{groupPath}.{fieldDef.Name}", baseByteOffset);

            currentBit = fieldDef.BitOffset + fieldDef.Bits;
        }

        return fields;
    }
}