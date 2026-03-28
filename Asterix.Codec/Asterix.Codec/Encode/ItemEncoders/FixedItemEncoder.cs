using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes a <see cref="FixedDecodedItem"/> into <paramref name="writer"/> according to
/// <paramref name="definition"/>.
///
/// <para>
/// Fields are written in <see cref="FixedItemDefinition.Fields"/> order.
/// Spare bits between consecutive fields (as declared by <see cref="FieldDefinition.BitOffset"/>)
/// are zero-padded. Spare bits after the last field up to the full item byte-length are also
/// zero-padded so that the item always occupies exactly <see cref="FixedItemDefinition.Length"/>
/// bytes.
/// </para>
/// </summary>
internal static class FixedItemEncoder
{
    internal static void Encode(
        BitWriter writer,
        FixedDecodedItem item,
        FixedItemDefinition definition,
        string itemPath)
    {
        // Use a local writer to accumulate exactly definition.Length bytes, then flush.
        // This makes spare-bit padding straightforward and ensures correct byte count.
        var local = new BitWriter(definition.Length);
        int currentBit = 0;

        for (int i = 0; i < definition.Fields.Count; i++)
        {
            FieldDefinition fieldDef = definition.Fields[i];
            string fieldPath = $"{itemPath}.{fieldDef.Name}";

            if (fieldDef.BitOffset > currentBit)
                local.WriteBits(0UL, fieldDef.BitOffset - currentBit);

            DecodedField field = item.GetField(fieldDef.Name)
                                 ?? throw new EncodeException(fieldPath,
                                     $"Fixed item '{itemPath}' is missing field '{fieldDef.Name}'");

            FieldEncoder.Encode(local, field, fieldDef, fieldPath);
            currentBit = fieldDef.BitOffset + fieldDef.Bits;
        }

        // Zero-pad any trailing spare bits to reach the declared byte boundary.
        int totalBits = definition.Length * 8;
        if (currentBit < totalBits)
            local.WriteBits(0UL, totalBits - currentBit);

        writer.WriteBytes(local.ToSpan());
    }
}