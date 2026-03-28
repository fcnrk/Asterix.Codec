using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes a <see cref="RepetitiveDecodedItem"/> into <paramref name="writer"/> according to
/// <paramref name="definition"/>.
///
/// <para>
/// Writes the count field (derived from the actual element count, not any stored raw value),
/// then encodes each element via <see cref="ItemEncoderDispatcher"/>.
/// </para>
/// </summary>
internal static class RepetitiveItemEncoder
{
    internal static void Encode(
        BitWriter writer,
        RepetitiveDecodedItem item,
        RepetitiveItemDefinition definition,
        string itemPath)
    {
        int count = item.Elements.Count;
        writer.WriteBits((ulong)count, definition.CountField.Bits);

        for (int i = 0; i < count; i++)
            ItemEncoderDispatcher.Encode(writer, item.Elements[i], definition.Element, $"{itemPath}[{i}]");
    }
}
