using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes a <see cref="VariableDecodedItem"/> into <paramref name="writer"/> according to
/// <paramref name="definition"/>.
///
/// <para>
/// For each group in <see cref="VariableDecodedItem.Groups"/>:
/// </para>
/// <list type="number">
///   <item>
///     Write the 7 data bits in declaration order using <see cref="FieldEncoder"/>,
///     with zero-padding for any spare bits between fields or before the FX position.
///   </item>
///   <item>
///     Write the FX bit: 1 if more groups follow, 0 if this is the last group.
///   </item>
/// </list>
///
/// <para>
/// Round-trip correctness: only the decoded groups are re-encoded. Groups that were
/// not present in the original wire data (because FX was 0 at some point) are not
/// synthesised. The encoder rebuilds the FX bits from the actual group count stored
/// in <see cref="VariableDecodedItem.Groups"/> rather than from any stored flag.
/// </para>
/// </summary>
internal static class VariableItemEncoder
{
    internal static void Encode(
        BitWriter writer,
        VariableDecodedItem item,
        VariableItemDefinition definition,
        string itemPath)
    {
        int groupCount = item.Groups.Count;

        for (int g = 0; g < groupCount; g++)
        {
            IReadOnlyList<DecodedField> group = item.Groups[g];
            bool isLast = g == groupCount - 1;
            string groupPath = $"{itemPath}[{g}]";

            if (g >= definition.Groups.Count)
                throw new EncodeException(groupPath,
                    $"Variable item '{itemPath}' has {groupCount} groups but the schema " +
                    $"only defines {definition.Groups.Count}");

            VariableGroupDefinition groupDef = definition.Groups[g];
            int currentBit = 0;

            for (int f = 0; f < groupDef.Fields.Count; f++)
            {
                FieldDefinition fieldDef = groupDef.Fields[f];
                string fieldPath = $"{groupPath}.{fieldDef.Name}";

                if (fieldDef.BitOffset > currentBit)
                    writer.WriteBits(0UL, fieldDef.BitOffset - currentBit);

                DecodedField? decoded = FindField(group, fieldDef.Name);
                if (decoded is null)
                    throw new EncodeException(fieldPath,
                        $"Variable item group {g} is missing field '{fieldDef.Name}'");

                FieldEncoder.Encode(writer, decoded, fieldDef, fieldPath);
                currentBit = fieldDef.BitOffset + fieldDef.Bits;
            }

            // Zero-pad remaining spare bits up to bit 6 (7 data bits, 0-indexed).
            if (currentBit < 7)
                writer.WriteBits(0UL, 7 - currentBit);

            // FX bit: 1 if more groups follow, 0 if last.
            writer.WriteBool(!isLast);
        }
    }

    private static DecodedField? FindField(IReadOnlyList<DecodedField> fields, string name)
    {
        foreach (var f in fields)
            if (f.Name == name) return f;
        return null;
    }
}
