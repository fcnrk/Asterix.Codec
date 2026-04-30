using Asterix.Codec.Binary;
using Asterix.Codec.Encode.ItemEncoders;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode;

/// <summary>
/// Generic SPF (Supplementary Field Package) encoder. Mirrors <c>SpfDecoder</c>.
///
/// <para>
/// Encoding follows the mandatory structural order per CLAUDE.md §5:
/// </para>
/// <list type="number">
///   <item>Length field — written to the main writer with the computed total block size</item>
///   <item>Count fields — derived from the actual element count of the corresponding repetitive entry</item>
///   <item>Repetitive blocks — encoded from <see cref="SpfDecodedItem.GetRepetitive"/></item>
///   <item>Presence flags — recomputed from which optional fields are actually present</item>
///   <item>Conditional fields — encoded only when present</item>
/// </list>
///
/// <para>
/// Because the length field comes first on the wire but its value depends on all subsequent
/// bytes, a temporary <see cref="BitWriter"/> accumulates the post-length payload. Once all
/// entries are encoded, the total is: <c>lengthFieldBytes + temp.ByteLength</c>. That total
/// is written to the main writer first, followed by the temp payload.
/// </para>
/// </summary>
public static class SpfEncoder
{
    /// <summary>
    /// Encodes <paramref name="item"/> into <paramref name="writer"/> according to
    /// <paramref name="definition"/>.
    /// </summary>
    public static void Encode(
        BitWriter writer,
        SpfDecodedItem item,
        SpfFieldSetDefinition definition)
    {
        ScalarEntry? lengthEntry = null;
        foreach (SpfStructureEntry entry in definition.Structure)
        {
            if (entry is ScalarEntry s)
            {
                lengthEntry = s;
                break;
            }
        }

        if (lengthEntry is null)
            throw new EncodeException(definition.Name,
                $"SPF definition '{definition.Name}' has no leading ScalarEntry (length field)");

        int lengthFieldBytes = (lengthEntry.Bits + 7) / 8;

        var countOverrides = BuildCountOverrides(item, definition);

        var temp = new BitWriter();
        bool pastLengthEntry = false;

        foreach (SpfStructureEntry entry in definition.Structure)
        {
            if (!pastLengthEntry)
            {
                if (ReferenceEquals(entry, lengthEntry))
                    pastLengthEntry = true;
                continue;
            }

            EncodeEntry(temp, item, entry, definition.Name, countOverrides);
        }

        // Write length then payload.
        int totalBytes = lengthFieldBytes + temp.ByteLength;
        writer.WriteBits((ulong)totalBytes, lengthEntry.Bits);
        writer.WriteBytes(temp.ToSpan());
    }

    #region Helpers

    /// <summary>
    /// Builds a map from count_ref name → actual element count, so that when a
    /// <see cref="ScalarEntry"/> serving as a count is encoded, we use the real count
    /// rather than whatever value was stored in the decoded item.
    /// </summary>
    private static Dictionary<string, ulong> BuildCountOverrides(
        SpfDecodedItem item,
        SpfFieldSetDefinition definition)
    {
        var overrides = new Dictionary<string, ulong>(StringComparer.Ordinal);

        foreach (SpfStructureEntry entry in definition.Structure)
        {
            if (entry is SpfRepetitiveEntry rep)
            {
                IReadOnlyList<SpfGroupValue>? elements = item.GetRepetitive(rep.Name);
                int count = elements?.Count ?? 0;
                overrides[rep.CountRef] = (ulong)count;
            }
        }

        return overrides;
    }

    private static void EncodeEntry(
        BitWriter writer,
        SpfDecodedItem item,
        SpfStructureEntry entry,
        string definitionName,
        Dictionary<string, ulong> countOverrides)
    {
        switch (entry)
        {
            case ScalarEntry scalar:
                ulong value = countOverrides.TryGetValue(scalar.Name, out ulong overridden)
                    ? overridden
                    : (item.GetScalar(scalar.Name)
                       ?? throw new EncodeException($"{definitionName}.{scalar.Name}",
                           $"Scalar '{scalar.Name}' not found in SPF decoded item"));
                writer.WriteBits(value, scalar.Bits);
                break;
            case SpfRepetitiveEntry rep:
                IReadOnlyList<SpfGroupValue>? elements = item.GetRepetitive(rep.Name);
                if (elements is null)
                    throw new EncodeException($"{definitionName}.{rep.Name}",
                        $"Repetitive entry '{rep.Name}' not found in SPF decoded item");

                EncodeRepetitive(writer, elements, rep, $"{definitionName}.{rep.Name}");
                break;
            case DynamicPresenceEntry presenceEntry:
                EncodePresence(writer, item, presenceEntry);
                break;
            case OptionalEntry optional:
                DecodedField? field = item.GetOptional(optional.Name);
                if (field is not null)
                    FieldEncoder.Encode(writer, field, optional.Field, $"{definitionName}.{optional.Name}");
                break;
            case OptionalGroupEntry optGroup:
                SpfGroupValue? grpValue = item.GetOptionalGroup(optGroup.Name);
                if (grpValue is not null)
                    EncodeGroupElement(writer, grpValue, optGroup.Fields,
                        $"{definitionName}.{optGroup.Name}");
                break;
            case OptionalRepetitiveEntry optRep:
                SpfOptionalRepetitiveValue? optRepValue = item.GetOptionalRepetitive(optRep.Name);
                if (optRepValue is not null)
                    EncodeOptionalRepetitive(writer, optRepValue, optRep,
                        $"{definitionName}.{optRep.Name}");
                break;
            default:
                throw new EncodeException(definitionName,
                    $"Unknown SPF structure entry type '{entry.GetType().Name}'");
        }
    }

    private static void EncodeRepetitive(
        BitWriter writer,
        IReadOnlyList<SpfGroupValue> elements,
        SpfRepetitiveEntry entry,
        string path)
    {
        for (int i = 0; i < elements.Count; i++)
            EncodeGroupElement(writer, elements[i], entry.Element.Fields, $"{path}[{i}]");
    }

    private static void EncodePresence(
        BitWriter writer,
        SpfDecodedItem item,
        DynamicPresenceEntry entry)
    {
        // Prefer the stored decoded flag value (preserves original wire value for round-trip).
        // Fall back to 1 (present) / 0 (absent) derived from whether the optional field exists.
        IReadOnlyDictionary<string, ulong>? stored = item.GetPresenceFlags(entry.Name);

        foreach (string fieldName in entry.Fields)
        {
            ulong flagValue;

            if (stored is not null && stored.TryGetValue(fieldName, out ulong storedFlag))
                flagValue = storedFlag;
            else
                flagValue = item.GetOptional(fieldName) is not null
                         || item.GetOptionalGroup(fieldName) is not null
                         || item.GetOptionalRepetitive(fieldName) is not null
                            ? 1UL : 0UL;

            writer.WriteBits(flagValue, entry.BitWidth);
        }
    }

    private static void EncodeGroupElement(
        BitWriter writer,
        SpfGroupValue group,
        IReadOnlyList<FieldDefinition> fields,
        string path)
    {
        int currentBit = 0;

        for (int i = 0; i < fields.Count; i++)
        {
            FieldDefinition fieldDef = fields[i];
            string fieldPath = $"{path}.{fieldDef.Name}";

            if (fieldDef.BitOffset > currentBit)
                writer.WriteBits(0UL, fieldDef.BitOffset - currentBit);

            DecodedField? field = group.GetField(fieldDef.Name);
            if (field is null)
                throw new EncodeException(fieldPath,
                    $"Group element missing field '{fieldDef.Name}'");

            FieldEncoder.Encode(writer, field, fieldDef, fieldPath);
            currentBit = fieldDef.BitOffset + fieldDef.Bits;
        }
    }

    private static void EncodeOptionalRepetitive(
        BitWriter writer,
        SpfOptionalRepetitiveValue value,
        OptionalRepetitiveEntry entry,
        string path)
    {
        if (value.Count != value.Elements.Count)
            throw new EncodeException(path,
                $"SpfOptionalRepetitiveValue '{path}': Count={value.Count} does not match Elements.Count={value.Elements.Count}");

        writer.WriteBits(value.Count, 8); // implicit uint8 count

        for (int i = 0; i < value.Elements.Count; i++)
            EncodeGroupElement(writer, value.Elements[i], entry.Element.Fields, $"{path}[{i}]");
    }

    #endregion
}