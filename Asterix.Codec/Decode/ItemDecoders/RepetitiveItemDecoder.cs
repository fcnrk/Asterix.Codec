using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes a <see cref="RepetitiveItemDefinition"/> from a <see cref="BitReader"/>.
///
/// <para>
/// Reads an inline count field (<see cref="RepetitiveItemDefinition.CountField"/>) then
/// decodes <see cref="RepetitiveItemDefinition.Element"/> exactly that many times.
/// The element definition may itself be any <see cref="ItemDefinition"/> subtype.
/// </para>
/// </summary>
internal static class RepetitiveItemDecoder
{
    internal static RepetitiveDecodedItem Decode(
        ref BitReader reader,
        RepetitiveItemDefinition definition,
        string itemPath,
        DecodeMode mode)
    {
        int count;
        try
        {
            count = (int)reader.ReadBits(definition.CountField.Bits);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, itemPath,
                $"Failed to read repetitive count field ({definition.CountField.Bits} bits)", ex);
        }

        var elements = new DecodedItem[count];

        for (int i = 0; i < count; i++)
        {
            string elementPath = $"{itemPath}[{i}]";
            elements[i] = ItemDecoderDispatcher.Decode(ref reader, definition.Element, elementPath, mode);
        }

        return new RepetitiveDecodedItem(elements);
    }
}