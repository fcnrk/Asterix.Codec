using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Shared single-field encoder used by both <see cref="FixedItemEncoder"/> and
/// <see cref="SpfEncoder"/>.
///
/// <para>
/// Always encodes from <see cref="DecodedField.RawValue"/> (the original bit pattern)
/// to guarantee round-trip correctness. <see cref="DecodedField.ScaledValue"/> is
/// intentionally ignored.
/// </para>
/// </summary>
internal static class FieldEncoder
{
    internal static void Encode(BitWriter writer, DecodedField field, FieldDefinition fieldDef, string fieldPath)
    {
        try
        {
            switch (fieldDef.Type)
            {
                case FieldType.UInt:
                    writer.WriteBits(field.RawValue, fieldDef.Bits);
                    break;
                case FieldType.Int:
                    writer.WriteSignedBits((long)field.RawValue, fieldDef.Bits);
                    break;
                case FieldType.Bool:
                    writer.WriteBool(field.RawValue != 0);
                    break;
                case FieldType.String:
                    var byteLen = fieldDef.StringLength
                        ?? throw new EncodeException(fieldPath, $"StringLength is null for string field '{fieldDef.Name}'");

                    var value = field.StringValue
                        ?? throw new EncodeException(fieldPath, $"StringValue is null for string field '{fieldDef.Name}'");

                    switch (fieldDef.Encoding)
                    {
                        case StringEncoding.Ia5:
                            StringEncoders.EncodeIa5(value, byteLen, writer);
                            break;
                        case StringEncoding.Ascii:
                            StringEncoders.EncodeAscii(value, byteLen, writer);
                            break;
                        default:
                            throw new EncodeException(fieldPath, $"Unknown string encoding '{fieldDef.Encoding}' for field '{fieldDef.Name}'");
                    }
                    break;
                default:
                    throw new EncodeException(fieldPath,
                        $"Unsupported field type '{fieldDef.Type}' for field '{fieldDef.Name}'");
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new EncodeException(fieldPath, ex.Message, ex);
        }
    }
}
