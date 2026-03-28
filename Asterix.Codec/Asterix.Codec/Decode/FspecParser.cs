using Asterix.Codec.Binary;

namespace Asterix.Codec.Decode;

/// <summary>
/// Reads and interprets ASTERIX FSPEC (Field Specification) bytes.
///
/// <para>
/// FSPEC is a variable-length prefix on every ASTERIX record that indicates which data items
/// are present. Its structure:
/// </para>
///
/// <code>
/// Byte layout (each FSPEC byte):
///
///   bit 7  bit 6  bit 5  bit 4  bit 3  bit 2  bit 1  bit 0
///  ┌──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┐
///  │  P1  │  P2  │  P3  │  P4  │  P5  │  P6  │  P7  │  FX  │
///  └──────┴──────┴──────┴──────┴──────┴──────┴──────┴──────┘
///    UAP    UAP    UAP    UAP    UAP    UAP    UAP    extension
///   pos+0  pos+1  pos+2  pos+3  pos+4  pos+5  pos+6   bit
///
///  FX = 1 → another FSPEC byte follows
///  FX = 0 → this is the last FSPEC byte
/// </code>
///
/// <para>
/// Each byte contributes 7 presence bits. Bit 7 (MSB) maps to the next sequential UAP position;
/// bit 1 maps to the 7th position within the byte; bit 0 is the FX extension flag.
/// </para>
///
/// <para>
/// This class is category-agnostic: it knows nothing about UAP structure. The caller maps
/// the returned presence array to item IDs via <see cref="GetPresentItemIds"/>.
/// </para>
/// </summary>
public static class FspecParser
{
    /// <summary>
    /// Data bits per FSPEC byte (bits 7..1; bit 0 is FX).
    /// </summary>
    private const int DataBitsPerByte = 7;

    /// <summary>
    /// Maximum FSPEC bytes supported. 16 bytes × 7 bits = 112 UAP positions,
    /// which far exceeds any defined ASTERIX category.
    /// </summary>
    private const int MaxFspecBytes = 16;

    #region Read

    /// <summary>
    /// Reads FSPEC bytes from <paramref name="reader"/> until FX = 0, then returns a
    /// presence array indexed by UAP position (0-based, 0 = first UAP entry).
    ///
    /// <para>
    /// On return, <paramref name="reader"/> is positioned immediately after the last FSPEC byte,
    /// ready to read the first data item.
    /// </para>
    /// </summary>
    /// <param name="reader">
    /// A <see cref="BitReader"/> positioned at the first FSPEC byte. Must be byte-aligned.
    /// </param>
    /// <returns>
    /// <c>bool[]</c> of length <c>N × 7</c> where N is the number of FSPEC bytes read.
    /// <c>presence[i]</c> is <c>true</c> when UAP item at index <c>i</c> is present.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The buffer ends before FX = 0 is seen, or the FSPEC exceeds <see cref="MaxFspecBytes"/>.
    /// </exception>
    public static bool[] ReadPresence(ref BitReader reader)
    {
        if (!reader.IsAligned)
            throw new InvalidOperationException(
                $"FSPEC must start on a byte boundary; bit offset in byte is {reader.BitOffsetInByte}");

        Span<byte> fspecBytes = stackalloc byte[MaxFspecBytes]; //TODO: not sure, check.
        int byteCount = 0;

        while (true)
        {
            if (reader.RemainingBits < 8)
                throw new InvalidOperationException(
                    $"Unexpected end of data while reading FSPEC byte {byteCount + 1} " +
                    $"(byte offset {reader.ByteOffset})");

            byte b = (byte)reader.ReadBits(8);

            if (byteCount == MaxFspecBytes)
                throw new InvalidOperationException(
                    $"FSPEC exceeds maximum supported length of {MaxFspecBytes} bytes " +
                    $"(byte offset {reader.ByteOffset - MaxFspecBytes})");

            fspecBytes[byteCount++] = b;

            if ((b & 0x01) == 0) // FX = 0 → last byte
                break;
        }

        return BuildPresenceArray(fspecBytes.Slice(0, byteCount));
    }

    #endregion

    #region Map

    /// <summary>
    /// Maps a presence array to the ordered list of item IDs that are flagged present,
    /// using <paramref name="uap"/> as the UAP position → item ID mapping.
    ///
    /// <para>
    /// FSPEC presence bits beyond the end of <paramref name="uap"/> are silently ignored —
    /// a conforming encoder never sets bits for undefined UAP positions. Strict-mode
    /// checking of surplus bits is the responsibility of the caller.
    /// </para>
    /// </summary>
    /// <param name="presence">Presence flags from <see cref="ReadPresence"/>.</param>
    /// <param name="uap">
    /// Ordered UAP item IDs from <see cref="Schema.Models.MessageDefinition.Uap"/>.
    /// </param>
    /// <returns>
    /// Item IDs in UAP order whose presence flag is <c>true</c>.
    /// </returns>
    public static IReadOnlyList<string> GetPresentItemIds(
        bool[] presence,
        IReadOnlyList<string> uap)
    {
        int limit = Math.Min(presence.Length, uap.Count);

        int presentCount = 0;
        for (int i = 0; i < limit; i++)
            if (presence[i])
                presentCount++;

        if (presentCount == 0)
            return Array.Empty<string>();

        var result = new string[presentCount];
        int idx = 0;

        for (int i = 0; i < limit; i++)
        {
            if (presence[i])
                result[idx++] = uap[i];
        }

        return result;
    }

    /// <summary>
    /// Returns the number of FSPEC bytes that were consumed given a presence array
    /// (inverse of reading — used in encoding to verify symmetry).
    /// </summary>
    public static int ByteCount(bool[] presence) =>
        (presence.Length + DataBitsPerByte - 1) / DataBitsPerByte;

    #endregion

    #region Helpers

    /// <summary>
    /// Converts raw FSPEC bytes into a flat presence array.
    /// Bit 7 (MSB) of each byte → presence[baseIndex + 0].
    /// Bit 1           of each byte → presence[baseIndex + 6].
    /// Bit 0 (FX) is not included.
    /// </summary>
    private static bool[] BuildPresenceArray(ReadOnlySpan<byte> fspecBytes)
    {
        var presence = new bool[fspecBytes.Length * DataBitsPerByte];

        for (int b = 0; b < fspecBytes.Length; b++)
        {
            byte raw = fspecBytes[b];
            int baseIdx = b * DataBitsPerByte;

            // Bits 7 down to 1 → UAP positions baseIdx+0 … baseIdx+6
            for (int bit = 7; bit >= 1; bit--)
                presence[baseIdx + (7 - bit)] = ((raw >> bit) & 1) != 0;
        }

        return presence;
    }

    #endregion
}