using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes a <see cref="StructuredExplicitDecodedItem"/> to the wire format of a
/// <see cref="StructuredExplicitItemDefinition"/>.
///
/// <para>
/// Wire format:
/// <code>
///   byte 0     : LEN — total byte count including this byte
///   bytes 1..N : inner items encoded in content-list order
/// </code>
/// </para>
/// </summary>
internal static class StructuredExplicitItemEncoder
{
    internal static void Encode(
        BitWriter writer,
        StructuredExplicitDecodedItem decoded,
        StructuredExplicitItemDefinition def,
        string itemPath)
    {
        var contentWriter = new BitWriter();

        foreach (var entry in def.Content)
        {
            if (!decoded.Items.TryGetValue(entry.Id, out var item))
                throw new EncodeException(itemPath,
                    $"Structured-explicit item '{itemPath}': inner item '{entry.Id}' is missing from the decoded record.");

            ItemEncoderDispatcher.Encode(contentWriter, item, entry.Definition, entry.Id);
        }

        byte[] content = contentWriter.ToArray();
        writer.WriteBits((ulong)(content.Length + 1), 8); // LEN includes the length byte itself
        foreach (byte b in content)
            writer.WriteBits(b, 8);
    }
}
