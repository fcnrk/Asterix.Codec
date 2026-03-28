using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes a <see cref="StructuredExplicitItemDefinition"/> (seItem application data item)
/// from a <see cref="BitReader"/>.
///
/// <para>
/// Wire format:
/// <code>
///   byte 0     : LEN — total byte count including this byte (minimum 1)
///   bytes 1..N : content  (N = LEN − 1 bytes)
/// </code>
/// </para>
///
/// <para>
/// The content bytes are extracted into a local buffer and decoded using a sub-reader.
/// Each inner item is decoded in sequence using <see cref="ItemDecoderDispatcher"/>,
/// producing a <see cref="StructuredExplicitDecodedItem"/> keyed by inner-item Id.
/// </para>
/// </summary>
internal static class StructuredExplicitItemDecoder
{
    internal static StructuredExplicitDecodedItem Decode(
        ref BitReader reader,
        StructuredExplicitItemDefinition def,
        string itemPath,
        DecodeMode mode)
    {
        var startByte = reader.ByteOffset;

        int len;
        try
        {
            len = (int)reader.ReadBits(8);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(startByte, itemPath,
                $"Cannot read LEN byte for seItem item '{itemPath}'", ex);
        }

        if (len < 1)
            throw new DecodeException(startByte, itemPath,
                $"Structured-explicit item '{itemPath}' has LEN = {len}; minimum is 1");

        int contentByteCount = len - 1;
        var buf = new byte[contentByteCount];

        for (int i = 0; i < contentByteCount; i++)
        {
            try
            {
                buf[i] = (byte)reader.ReadBits(8);
            }
            catch (InvalidOperationException ex)
            {
                throw new DecodeException(startByte, itemPath,
                    $"Structured-explicit item '{itemPath}' declares LEN={len} but buffer underrun at byte {i + 1}", ex);
            }
        }

        var subReader = new BitReader(buf);
        var items = new Dictionary<string, DecodedItem>(def.Content.Count, StringComparer.Ordinal);

        foreach (var entry in def.Content)
            items[entry.Id] = ItemDecoderDispatcher.Decode(ref subReader, entry.Definition, entry.Id, mode);

        return new StructuredExplicitDecodedItem(items);
    }
}
