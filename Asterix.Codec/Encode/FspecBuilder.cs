using Asterix.Codec.Binary;

namespace Asterix.Codec.Encode;

/// <summary>
/// Builds ASTERIX FSPEC bytes from a set of present item IDs and a UAP definition.
///
/// <para>
/// This is the encode-side counterpart to <see cref="Decode.FspecParser"/>.
/// FSPEC bytes are always recomputed from the set of present items — they are
/// never stored in the decoded model — which is what guarantees round-trip correctness.
/// </para>
///
/// <para>
/// FSPEC byte layout (per byte):
/// </para>
/// <code>
///   bit 7  bit 6  bit 5  bit 4  bit 3  bit 2  bit 1  bit 0
///  ┌──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┐
///  │  P1  │  P2  │  P3  │  P4  │  P5  │  P6  │  P7  │  FX  │
///  └──────┴──────┴──────┴──────┴──────┴──────┴──────┴──────┘
///
///  FX = 1 on all bytes except the last.
///  FX = 0 on the last byte.
/// </code>
/// </summary>
public static class FspecBuilder
{
    private const int DataBitsPerByte = 7;

    /// <summary>
    /// Writes FSPEC bytes for <paramref name="presentItemIds"/> into <paramref name="writer"/>.
    ///
    /// <para>
    /// The minimum number of bytes required to represent the highest-indexed present item
    /// is computed, then each byte is built by checking presence for its 7 UAP positions.
    /// The FX bit is set on all bytes except the last.
    /// </para>
    ///
    /// <para>
    /// If no items are present, nothing is written (caller should ensure this
    /// does not produce a malformed record).
    /// </para>
    /// </summary>
    /// <param name="uap">
    /// Ordered UAP item IDs from <see cref="Schema.Models.MessageDefinition.Uap"/>.
    /// </param>
    /// <param name="presentItemIds">
    /// Set of item IDs that are present in the record being encoded.
    /// Lookup is O(1); <see cref="HashSet{T}"/> recommended.
    /// </param>
    /// <param name="writer">Destination; must be byte-aligned on entry.</param>
    /// <exception cref="ArgumentException">
    /// An item in <paramref name="presentItemIds"/> does not appear in <paramref name="uap"/>.
    /// </exception>
    public static void WriteFspec(
        IReadOnlyList<string> uap,
        ISet<string> presentItemIds,
        BitWriter writer)
    {
        if (!writer.IsAligned)
            throw new InvalidOperationException(
                $"FSPEC must start on a byte boundary; bit offset in byte is {writer.BitPosition & 7}");

        var numBytes = ComputeByteCount(uap, presentItemIds);

        if (numBytes == 0)
            return;

        for (int byteIdx = 0; byteIdx < numBytes; byteIdx++)
        {
            var fspecByte = BuildFspecByte(uap, presentItemIds, byteIdx);

            var isLastByte = byteIdx == numBytes - 1;
            if (!isLastByte)
                fspecByte |= 0x01; // FX = 1: more bytes follow

            writer.WriteBits(fspecByte, 8);
        }
    }

    /// <summary>
    /// Returns the FSPEC as a <c>byte[]</c> without a <see cref="BitWriter"/>.
    /// Convenience overload for testing and serialization utilities.
    /// </summary>
    public static byte[] BuildFspec(
        IReadOnlyList<string> uap,
        ISet<string> presentItemIds)
    {
        var numBytes = ComputeByteCount(uap, presentItemIds);

        if (numBytes == 0)
            return Array.Empty<byte>();

        var result = new byte[numBytes];

        for (int byteIdx = 0; byteIdx < numBytes; byteIdx++)
        {
            var fspecByte = BuildFspecByte(uap, presentItemIds, byteIdx);

            var isLastByte = byteIdx == numBytes - 1;
            if (!isLastByte)
                fspecByte |= 0x01;

            result[byteIdx] = fspecByte;
        }

        return result;
    }

    #region Helpers

    /// <summary>
    /// Determines how many FSPEC bytes are needed to represent all present items.
    /// The answer is <c>floor(highestUapIndex / 7) + 1</c>.
    /// Returns 0 if no items are present.
    /// </summary>
    private static int ComputeByteCount(
        IReadOnlyList<string> uap,
        ISet<string> presentItemIds)
    {
        var lastIndex = -1;

        for (int i = 0; i < uap.Count; i++)
            if (presentItemIds.Contains(uap[i]))
                lastIndex = i;

        return lastIndex < 0 ? 0 : lastIndex / DataBitsPerByte + 1;
    }

    /// <summary>
    /// Builds a single FSPEC byte for <paramref name="byteIdx"/> (0-based).
    /// Sets bits 7..1 for present UAP items; bit 0 (FX) is left 0 — caller sets it.
    /// </summary>
    private static byte BuildFspecByte(
        IReadOnlyList<string> uap,
        ISet<string> presentItemIds,
        int byteIdx)
    {
        byte fspecByte = 0;

        for (int bitIdx = 0; bitIdx < DataBitsPerByte; bitIdx++)
        {
            int uapIndex = byteIdx * DataBitsPerByte + bitIdx;

            if (uapIndex < uap.Count && presentItemIds.Contains(uap[uapIndex]))
            {
                // bitIdx 0 → bit 7 (MSB); bitIdx 6 → bit 1
                fspecByte |= (byte)(1 << (7 - bitIdx));
            }
        }

        return fspecByte;
    }
    #endregion
}