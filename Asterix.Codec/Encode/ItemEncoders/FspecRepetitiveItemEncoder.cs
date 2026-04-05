using Asterix.Codec.Binary;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes a <see cref="FspecRepetitiveDecodedItem"/> into <paramref name="writer"/>
/// according to <paramref name="definition"/>.
///
/// <para>
/// Writes an FSPEC prefix where every data bit is set (one bit per element),
/// with FX-bit extension for N &gt; 7. Then encodes each element via
/// <see cref="ItemEncoderDispatcher"/>.
/// </para>
/// </summary>
internal static class FspecRepetitiveItemEncoder
{
    private const int DataBitsPerByte = 7;

    internal static void Encode(
        BitWriter writer,
        FspecRepetitiveDecodedItem item,
        FspecRepetitiveItemDefinition definition,
        string itemPath)
    {
        int count = item.Elements.Count;
        WriteFspec(writer, count);

        for (int i = 0; i < count; i++)
            ItemEncoderDispatcher.Encode(
                writer, item.Elements[i], definition.Element, $"{itemPath}[{i}]");
    }

    /// <summary>
    /// Writes an FSPEC with exactly <paramref name="count"/> data bits set to 1.
    /// For count = 0, writes one zero byte (valid FSPEC with FX = 0).
    /// </summary>
    private static void WriteFspec(BitWriter writer, int count)
    {
        int fspecByteCount = count == 0 ? 1 : (count - 1) / DataBitsPerByte + 1;

        for (int byteIdx = 0; byteIdx < fspecByteCount; byteIdx++)
        {
            int firstElemInByte = byteIdx * DataBitsPerByte;
            int elemsInByte = Math.Min(DataBitsPerByte, count - firstElemInByte);
            bool isLast = byteIdx == fspecByteCount - 1;

            byte fspecByte = 0;
            for (int i = 0; i < elemsInByte; i++)
                fspecByte |= (byte)(1 << (7 - i));  // bit 7 = first element

            if (!isLast)
                fspecByte |= 0x01;  // FX = 1: more bytes follow

            writer.WriteBits(fspecByte, 8);
        }
    }
}
