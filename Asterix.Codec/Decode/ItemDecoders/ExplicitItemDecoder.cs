using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes an <see cref="ExplicitItemDefinition"/> (RE / SP field) from a <see cref="BitReader"/>.
///
/// <para>
/// Wire format:
/// <code>
///   byte 0     : LEN  — total byte count, including this byte (minimum value: 1)
///   bytes 1..N : content  (N = LEN − 1 bytes, may be empty when LEN = 1)
/// </code>
/// </para>
///
/// <para>
/// The content is stored verbatim as a <c>byte[]</c> in
/// <see cref="ExplicitDecodedItem.Content"/>. No field-level decoding is performed,
/// guaranteeing lossless round-trip preservation for RE/SP payloads.
/// </para>
/// </summary>
internal static class ExplicitItemDecoder
{
    internal static ExplicitDecodedItem Decode(
        ref BitReader reader,
        string itemPath)
    {
        int itemStartByte = reader.ByteOffset;

        // Read LEN byte.
        ulong lenRaw;
        try
        {
            lenRaw = reader.ReadBits(8);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(itemStartByte, itemPath,
                $"Cannot read LEN byte for explicit item '{itemPath}'", ex);
        }

        int len = (int)lenRaw;

        if (len < 1)
            throw new DecodeException(itemStartByte, itemPath,
                $"Explicit item '{itemPath}' has LEN = {len}; minimum is 1");

        int contentBytes = len - 1;

        if (contentBytes == 0)
            return new ExplicitDecodedItem(Array.Empty<byte>());

        ReadOnlySpan<byte> contentSpan;
        try
        {
            contentSpan = reader.ReadBytes(contentBytes);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(itemStartByte, itemPath,
                $"Explicit item '{itemPath}' declares LEN = {len} " +
                $"but only {reader.RemainingBits / 8 + 1} bytes available", ex);
        }

        return new ExplicitDecodedItem(contentSpan.ToArray());
    }
}
