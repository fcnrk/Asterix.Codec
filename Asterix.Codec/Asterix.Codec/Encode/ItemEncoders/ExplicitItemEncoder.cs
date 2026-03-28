using Asterix.Codec.Binary;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes an <see cref="ExplicitDecodedItem"/> (RE / SP field) into <paramref name="writer"/>.
///
/// <para>
/// Wire format:
/// <code>
///   byte 0     : LEN = Content.Length + 1  (includes the length byte itself)
///   bytes 1..N : Content verbatim
/// </code>
/// </para>
/// </summary>
internal static class ExplicitItemEncoder
{
    internal static void Encode(
        BitWriter writer,
        ExplicitDecodedItem item,
        ExplicitItemDefinition definition,
        string itemPath)
    {
        int len = item.Content.Length + 1;
        writer.WriteBits((ulong)len, 8);

        if (item.Content.Length > 0)
            writer.WriteBytes(item.Content);
    }
}
