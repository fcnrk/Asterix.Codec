using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Shared single-field decoder used by both <see cref="FixedItemDecoder"/> and
/// <see cref="SpfDecoder"/>.
///
/// <para>
/// <paramref name="baseByteOffset"/> bridges the gap between readers that are
/// positioned within a local slice (e.g. the per-item reader in
/// <see cref="FixedItemDecoder"/>) and the original packet byte offset needed for
/// accurate <see cref="DecodeException"/> context:
/// </para>
/// <code>
///   error byte offset = baseByteOffset + reader.ByteOffset
/// </code>
/// <list type="bullet">
///   <item>
///     <see cref="FixedItemDecoder"/>: pass the item's start offset in the packet
///     (<c>mainReader.ByteOffset</c> captured before <c>ReadBytes</c>). The local
///     reader's <c>ByteOffset</c> is within-item; adding the item start gives the
///     packet-absolute position.
///   </item>
///   <item>
///     <see cref="SpfDecoder"/>: pass <c>0</c>. The reader is the main packet reader
///     so <c>reader.ByteOffset</c> is already packet-absolute.
///   </item>
/// </list>
/// </summary>
internal static class FieldDecoder
{
    internal static DecodedField Decode(
        ref BitReader reader,
        FieldDefinition fieldDef,
        string fieldPath,
        int baseByteOffset)
    {
        try
        {
            return fieldDef.Type switch
            {
                FieldType.UInt => DecodeUInt(ref reader, fieldDef),
                FieldType.Int => DecodeInt(ref reader, fieldDef),
                FieldType.Bool => DecodeBool(ref reader, fieldDef),
                FieldType.String => DecodeString(ref reader, fieldDef),
                _ => throw new DecodeException(
                    baseByteOffset + reader.ByteOffset, fieldPath,
                    $"Unsupported field type '{fieldDef.Type}'")
            };
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(
                baseByteOffset + reader.ByteOffset, fieldPath, ex.Message, ex);
        }
    }

    #region Per-type decoders

    private static DecodedField DecodeUInt(ref BitReader reader, FieldDefinition fieldDef)
    {
        ulong raw = reader.ReadBits(fieldDef.Bits);
        double? scaled = fieldDef.Scale.HasValue ? raw * fieldDef.Scale.Value.Value : null;
        return new DecodedField(fieldDef.Name, raw, scaled, null);
    }

    private static DecodedField DecodeInt(ref BitReader reader, FieldDefinition fieldDef)
    {
        long signed = reader.ReadSignedBits(fieldDef.Bits);
        ulong raw = (ulong)signed;
        double? scaled = fieldDef.Scale.HasValue ? signed * fieldDef.Scale.Value.Value : null;
        return new DecodedField(fieldDef.Name, raw, scaled, null);
    }

    private static DecodedField DecodeBool(ref BitReader reader, FieldDefinition fieldDef)
    {
        ulong raw = reader.ReadBits(1);
        return new DecodedField(fieldDef.Name, raw, null, null);
    }

    private static DecodedField DecodeString(ref BitReader reader, FieldDefinition fieldDef)
    {
        int byteLen = fieldDef.StringLength
                      ?? throw new InvalidOperationException(
                          $"StringLength is null for string field '{fieldDef.Name}'");

        ReadOnlySpan<byte> strBytes = reader.ReadBytes(byteLen);

        string value = fieldDef.Encoding switch
        {
            StringEncoding.Ia5 => StringEncoders.DecodeIa5(strBytes),
            StringEncoding.Ascii => StringEncoders.DecodeAscii(strBytes),
            _ => throw new InvalidOperationException(
                $"Unknown string encoding '{fieldDef.Encoding}' " +
                $"for field '{fieldDef.Name}'")
        };

        return new DecodedField(fieldDef.Name, 0, null, value);
    }

    #endregion
}