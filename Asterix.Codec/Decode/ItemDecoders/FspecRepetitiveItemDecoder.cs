using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes a <see cref="FspecRepetitiveItemDefinition"/> from a <see cref="BitReader"/>.
///
/// <para>
/// Reads an inner FSPEC (with FX-bit extension, identical to
/// <see cref="CompoundItemDecoder"/>) and counts the number of set data bits.
/// That count determines how many consecutive instances of
/// <see cref="FspecRepetitiveItemDefinition.Element"/> are decoded.
/// </para>
/// </summary>
internal static class FspecRepetitiveItemDecoder
{
    internal static FspecRepetitiveDecodedItem Decode(
        ref BitReader reader,
        FspecRepetitiveItemDefinition definition,
        string itemPath,
        DecodeMode mode)
    {
        // FspecParser.ReadPresence caps at MaxFspecBytes (16 bytes = 112 elements).
        // That limit was sized for record-level FSPECs and technically applies here too,
        // but 112 contributing systems exceeds any realistic deployment. Accepted as-is.
        bool[] presence;
        try
        {
            presence = FspecParser.ReadPresence(ref reader);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, itemPath,
                "Failed to read inner FSPEC for fspec_repetitive item", ex);
        }

        // Count set data bits — each one means one element is present.
        int count = 0;
        foreach (bool b in presence)
            if (b) count++;

        var elements = new DecodedItem[count];
        for (int i = 0; i < count; i++)
        {
            string elementPath = $"{itemPath}[{i}]";
            elements[i] = ItemDecoderDispatcher.Decode(
                ref reader, definition.Element, elementPath, mode);
        }

        return new FspecRepetitiveDecodedItem(elements);
    }
}
