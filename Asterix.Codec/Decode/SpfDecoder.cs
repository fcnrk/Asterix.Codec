using Asterix.Codec.Binary;
using Asterix.Codec.Decode.ItemDecoders;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode;

/// <summary>
/// Generic SPF (Supplementary Field Package) decoder.
///
/// <para>
/// All behaviour is driven entirely by <see cref="SpfFieldSetDefinition"/>; no CAT-specific
/// or SPF-layout-specific logic is hardcoded here.
/// </para>
///
/// <para>
/// Mandatory decode order per CLAUDE.md §5 (enforced by the order of entries in
/// <see cref="SpfFieldSetDefinition.Structure"/>):
/// </para>
/// <list type="number">
///   <item>Length field (<see cref="ScalarEntry"/> — first entry, bounds the block)</item>
///   <item>Count fields (<see cref="ScalarEntry"/> — stored in <see cref="DecodeContext"/>)</item>
///   <item>Repetitive blocks (<see cref="SpfRepetitiveEntry"/> — count resolved from context)</item>
///   <item>Presence flags (<see cref="DynamicPresenceEntry"/> — fills context with dot-path keys)</item>
///   <item>Conditional fields (<see cref="OptionalEntry"/> — decoded only when presence flag is set)</item>
/// </list>
///
/// <para>
/// The decode order is implicit in the structure list — the decoder processes it linearly
/// without reordering. <c>SchemaValidator</c> enforces that all backward-references
/// (<c>count_ref</c>, <c>present_if</c>) resolve to entries earlier in the structure.
/// </para>
/// </summary>
public static class SpfDecoder
{
    /// <summary>
    /// Decodes one SPF block from <paramref name="reader"/>.
    ///
    /// <para>
    /// On entry, <paramref name="reader"/> must be positioned at the first byte of the
    /// SPF block (i.e., the first byte of the length field). On exit, it is positioned
    /// immediately after the last byte of the SPF block, as declared by the length field.
    /// </para>
    /// </summary>
    /// <param name="reader">Main packet reader. Used directly — no local slice.</param>
    /// <param name="definition">The validated SPF field set definition from <c>SchemaRegistry</c>.</param>
    /// <param name="mode">Strict: throws on mismatch. Lenient: clamps to declared length.</param>
    public static SpfDecodedItem Decode(
        ref BitReader reader,
        SpfFieldSetDefinition definition,
        DecodeMode mode)
    {
        int blockStartBit = reader.BitPosition;
        int blockEndBit   = -1;  // set when first ScalarEntry (length) is decoded

        var context      = new DecodeContext();
        var resultFields = new Dictionary<string, object?>(definition.Structure.Count);

        foreach (SpfStructureEntry entry in definition.Structure)
        {
            // Block boundary guard (strict only)
            if (blockEndBit >= 0 && mode == DecodeMode.Strict && reader.BitPosition > blockEndBit)
            {
                throw new DecodeException(reader.ByteOffset, entry.Name,
                    $"SPF entry '{entry.Name}' starts at bit {reader.BitPosition} which is " +
                    $"beyond the declared block end at bit {blockEndBit} " +
                    $"(block start {blockStartBit}, length {(blockEndBit - blockStartBit) / 8} bytes)");
            }

            switch (entry)
            {
                case ScalarEntry scalar:
                    ulong raw = DecodeScalar(ref reader, scalar);
                    context.Set(scalar.Name, raw);
                    resultFields[scalar.Name] = raw;

                    // The first scalar in every SPF structure is the length field.
                    // It declares the total byte size of the entire SPF block
                    // (including the length field bytes themselves).
                    if (blockEndBit < 0)
                        blockEndBit = blockStartBit + (int)raw * 8;
                    break;
                case SpfRepetitiveEntry rep:
                    resultFields[rep.Name] = DecodeRepetitive(ref reader, rep, context, mode);
                    break;
                case DynamicPresenceEntry presenceEntry:
                    resultFields[presenceEntry.Name] =
                        DecodePresence(ref reader, presenceEntry, context);
                    break;
                case OptionalEntry optional:
                    // null stored in resultFields when the field is absent.
                    resultFields[optional.Name] = DecodeOptional(ref reader, optional, context);
                    break;
                case OptionalGroupEntry optGroup:
                    resultFields[optGroup.Name] = DecodeOptionalGroup(ref reader, optGroup, context);
                    break;
                case OptionalRepetitiveEntry optRep:
                    resultFields[optRep.Name] = DecodeOptionalRepetitive(ref reader, optRep, context, mode);
                    break;
                default:
                    throw new DecodeException(reader.ByteOffset, entry.Name,
                        $"Unknown SPF structure entry type '{entry.GetType().Name}'");
            }
        }

        EnforceLengthBoundary(ref reader, definition.Name, blockStartBit, blockEndBit, mode);

        return new SpfDecodedItem(resultFields);
    }

    #region Per-entry decoders

    /// <summary>
    /// Reads a scalar (uint or int) from the reader and stores it in context.
    /// </summary>
    private static ulong DecodeScalar(ref BitReader reader, ScalarEntry entry)
    {
        try
        {
            return entry.Type == FieldType.Int
                ? (ulong)reader.ReadSignedBits(entry.Bits)
                : reader.ReadBits(entry.Bits);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, entry.Name,
                $"Failed to read scalar '{entry.Name}' ({entry.Bits} bits): {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Decodes a repetitive structure:
    /// resolves the count from <paramref name="context"/> via <c>count_ref</c>,
    /// then decodes exactly that many group elements.
    /// </summary>
    private static IReadOnlyList<SpfGroupValue> DecodeRepetitive(
        ref BitReader reader,
        SpfRepetitiveEntry entry,
        DecodeContext context,
        DecodeMode mode)
    {
        int count;
        try
        {
            ulong raw = context.Get(entry.CountRef);
            count = (int)raw;
        }
        catch (InvalidOperationException ex)
        {
            // count_ref names an entry that has not yet been decoded.
            // SchemaValidator prevents this for valid schemas; this path means there's a validator bug.
            throw new DecodeException(reader.ByteOffset, entry.Name,
                $"count_ref '{entry.CountRef}' not found in decode context. " +
                $"Ensure '{entry.CountRef}' appears before '{entry.Name}' in the SPF structure.", ex);
        }

        var elements = new SpfGroupValue[count];

        for (int i = 0; i < count; i++)
            elements[i] = DecodeGroupElement(ref reader, entry.Element, $"{entry.Name}[{i}]");

        return elements;
    }

    /// <summary>
    /// Decodes one element of a repetitive group.
    /// Fields are decoded in order; <see cref="FieldDefinition.BitOffset"/> drives
    /// <see cref="BitReader.Skip"/> calls for any spare bits between fields.
    /// </summary>
    private static SpfGroupValue DecodeGroupElement(
        ref BitReader reader,
        SpfElementDefinition element,
        string path)
    {
        var fields = new DecodedField[element.Fields.Count];
        int currentBit = 0;

        for (int i = 0; i < element.Fields.Count; i++)
        {
            FieldDefinition fieldDef = element.Fields[i];

            if (fieldDef.BitOffset > currentBit)
                reader.Skip(fieldDef.BitOffset - currentBit);

            fields[i] = FieldDecoder.Decode(ref reader, fieldDef, $"{path}.{fieldDef.Name}", 0);
            currentBit = fieldDef.BitOffset + fieldDef.Bits;
        }

        return new SpfGroupValue(fields);
    }

    /// <summary>
    /// Reads one presence flag per field in <paramref name="entry"/>.
    ///
    /// <para>
    /// Each flag is <see cref="DynamicPresenceEntry.BitWidth"/> bits wide.
    /// Zero = absent, non-zero = present.
    /// </para>
    ///
    /// <para>
    /// Flags are stored in <paramref name="context"/> under the composite key
    /// <c>"&lt;entry.Name&gt;.&lt;fieldName&gt;"</c> (e.g. <c>"presence.f4"</c>).
    /// This key matches the pre-split <see cref="OptionalEntry.PresenceGroup"/> +
    /// <see cref="OptionalEntry.PresenceField"/> that <c>DecodeContext.IsPresent</c> uses,
    /// so <c>present_if</c> evaluation requires zero string concatenation at runtime.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, ulong> DecodePresence(
        ref BitReader reader,
        DynamicPresenceEntry entry,
        DecodeContext context)
    {
        var flags = new Dictionary<string, ulong>(entry.Fields.Count, StringComparer.Ordinal);

        foreach (string fieldName in entry.Fields)
        {
            ulong flagValue;
            try
            {
                flagValue = reader.ReadBits(entry.BitWidth);
            }
            catch (InvalidOperationException ex)
            {
                throw new DecodeException(reader.ByteOffset, $"{entry.Name}.{fieldName}",
                    $"Failed to read presence flag for field '{fieldName}' " +
                    $"({entry.BitWidth} bits): {ex.Message}", ex);
            }

            // Key format must match DecodeContext.IsPresent: "groupName.fieldName"
            context.Set($"{entry.Name}.{fieldName}", flagValue);
            flags[fieldName] = flagValue;
        }

        return flags;
    }

    /// <summary>
    /// Decodes a conditionally present field.
    ///
    /// <para>
    /// The condition is evaluated by calling
    /// <see cref="DecodeContext.IsPresent(string, string)"/> with the pre-split
    /// <see cref="OptionalEntry.PresenceGroup"/> and <see cref="OptionalEntry.PresenceField"/>.
    /// No string parsing occurs at this point — the split was done once at load time.
    /// </para>
    ///
    /// Returns <c>null</c> when the field is absent; the caller stores <c>null</c>
    /// in <see cref="SpfDecodedItem.Fields"/> for the entry's name.
    /// </summary>
    private static DecodedField? DecodeOptional(
        ref BitReader reader,
        OptionalEntry entry,
        DecodeContext context)
    {
        // present_if evaluation: O(1) dictionary lookup in DecodeContext,
        // using the pre-split group + field names stored at load time.
        if (!context.IsPresent(entry.PresenceGroup, entry.PresenceField))
            return null;

        return FieldDecoder.Decode(ref reader, entry.Field, entry.Name, baseByteOffset: 0);
    }

    /// <summary>
    /// Decodes a conditionally present group of fields.
    ///
    /// <para>
    /// Returns <c>null</c> when absent; stores a <see cref="SpfGroupValue"/> when present.
    /// </para>
    /// </summary>
    private static SpfGroupValue? DecodeOptionalGroup(
        ref BitReader reader,
        OptionalGroupEntry entry,
        DecodeContext context)
    {
        if (!context.IsPresent(entry.PresenceGroup, entry.PresenceField))
            return null;

        return DecodeGroupElement(ref reader, entry.Element, entry.Name);
    }

    /// <summary>
    /// Decodes a conditionally present repetitive group.
    ///
    /// <para>
    /// Returns <c>null</c> when absent; reads an implicit <see cref="uint8"/> count and decodes
    /// that many group elements.
    /// </para>
    /// </summary>
    private static SpfOptionalRepetitiveValue? DecodeOptionalRepetitive(
        ref BitReader reader,
        OptionalRepetitiveEntry entry,
        DecodeContext context,
        DecodeMode mode)
    {
        if (!context.IsPresent(entry.PresenceGroup, entry.PresenceField))
            return null;

        byte count;
        try
        {
            count = (byte)reader.ReadBits(8);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, entry.Name,
                $"Failed to read implicit count for optional_repetitive '{entry.Name}': {ex.Message}", ex);
        }

        var elements = new SpfGroupValue[count];
        for (int i = 0; i < count; i++)
            elements[i] = DecodeGroupElement(ref reader, entry.Element, $"{entry.Name}[{i}]");

        return new SpfOptionalRepetitiveValue(count, elements);
    }
    #endregion


    private static void EnforceLengthBoundary(
        ref BitReader reader,
        string definitionName,
        int blockStartBit,
        int blockEndBit,
        DecodeMode mode)
    {
        if (blockEndBit < 0)
            return; // no length field was decoded — nothing to enforce

        int declaredBytes  = (blockEndBit - blockStartBit) / 8;
        int consumedBits   = reader.BitPosition - blockStartBit;
        int consumedBytes  = consumedBits / 8;

        if (reader.BitPosition == blockEndBit)
            return; // exact match — nothing to do

        if (mode == DecodeMode.Strict)
        {
            throw new DecodeException(reader.ByteOffset, definitionName,
                $"SPF block '{definitionName}' length mismatch: " +
                $"declared {declaredBytes} bytes but consumed {consumedBytes} bytes " +
                $"(bit positions: start={blockStartBit}, expected end={blockEndBit}, " +
                $"actual end={reader.BitPosition})");
        }

        // Lenient: seek to the declared block end.
        // This handles both under-read (future/unknown fields) and over-read recovery.
        reader.SetPosition(blockEndBit);
    }
}
