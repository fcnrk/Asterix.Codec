namespace Asterix.Codec.Binary;

/// <summary>
/// A forward-only, MSB-first bit reader over a <see cref="ReadOnlySpan{T}"/>.
///
/// <para>
/// ASTERIX is big-endian and MSB-first throughout: bit 0 of a field is the most significant bit
/// of the first byte. This reader enforces that convention for every read operation.
/// </para>
///
/// <para>
/// Declared as a <c>ref struct</c> so it can hold a <see cref="ReadOnlySpan{byte}"/> without
/// allocating. Callers must pass it by <c>ref</c> across method boundaries to propagate position
/// changes:
/// <code>
///   var reader = new BitReader(data);
///   DecodeItem(ref reader, definition);
/// </code>
/// </para>
///
/// All methods throw <see cref="InvalidOperationException"/> on overflow — never silently
/// truncate or wrap. This ensures strict-mode decoders catch malformed input immediately.
/// </summary>
public ref struct BitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _bitPosition; // absolute bit index from start; 0 = MSB of byte 0

    public BitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _bitPosition = 0;
    }

    /// <summary>
    /// Absolute bit index from the start of the buffer (0 = MSB of byte 0).
    /// </summary>
    public readonly int BitPosition => _bitPosition;

    /// <summary>
    /// Index of the byte currently being read from.
    /// </summary>
    public readonly int ByteOffset => _bitPosition >> 3;

    /// <summary>
    /// Bit offset within the current byte (0 = MSB, 7 = LSB).
    /// </summary>
    public readonly int BitOffsetInByte => _bitPosition & 7;

    /// <summary>
    /// Total bits remaining in the buffer.
    /// </summary>
    public readonly int RemainingBits => (_data.Length << 3) - _bitPosition;

    /// <summary>
    /// True when the current position is on an exact byte boundary.
    /// </summary>
    public readonly bool IsAligned => (_bitPosition & 7) == 0;

    /// <summary>
    /// Reads <paramref name="count"/> bits (1–64) as an unsigned value, MSB first.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not in [1, 64].</exception>
    /// <exception cref="InvalidOperationException">Fewer than <paramref name="count"/> bits remain.</exception>
    public ulong ReadBits(int count)
    {
        if ((uint)(count - 1) > 63u)
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must be 1–64");
        if (count > RemainingBits)
            throw new InvalidOperationException(
                $"Cannot read {count} bits: only {RemainingBits} remain at byte offset {ByteOffset}");

        ulong result = 0;
        int remaining = count;

        while (remaining > 0)
        {
            int byteIndex = _bitPosition >> 3;
            int bitInByte = _bitPosition & 7; // 0 = MSB of this byte
            int available = 8 - bitInByte; // bits left in this byte
            int take = remaining < available ? remaining : available;

            // Right-shift the byte so the desired bits are at its LSB, then mask.
            int shift = available - take;
            byte chunk = (byte)((_data[byteIndex] >> shift) & ((1 << take) - 1));

            result = (result << take) | chunk;
            _bitPosition += take;
            remaining -= take;
        }

        return result;
    }

    /// <summary>
    /// Reads <paramref name="count"/> bits as a sign-extended <see cref="long"/>.
    /// Negative values use two's-complement representation.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not in [1, 64].</exception>
    public long ReadSignedBits(int count)
    {
        if (count < 1 || count > 64)
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must be 1–64");

        ulong raw = ReadBits(count);

        if (count == 64)
            return (long)raw;

        // If the MSB of the field is 1, fill the upper (64 - count) bits with 1s.
        ulong signBit = 1UL << (count - 1);
        if ((raw & signBit) != 0)
            raw |= ~((1UL << count) - 1);

        return (long)raw;
    }

    /// <summary>
    /// Reads one bit as a boolean (<c>true</c> = 1).
    /// </summary>
    public bool ReadBool() => ReadBits(1) != 0;

    /// <summary>
    /// Returns a zero-copy slice of <paramref name="count"/> bytes from the current position
    /// and advances past them. Requires byte alignment.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not byte-aligned, or too few bytes remain.</exception>
    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (!IsAligned)
            throw new InvalidOperationException(
                $"ReadBytes requires byte alignment; bit offset in current byte is {BitOffsetInByte}");
        if ((uint)count > (uint)(_data.Length - ByteOffset))
            throw new InvalidOperationException(
                $"Cannot read {count} bytes: only {_data.Length - ByteOffset} remain at byte offset {ByteOffset}");

        var slice = _data.Slice(ByteOffset, count);
        _bitPosition += count << 3;
        return slice;
    }

    /// <summary>
    /// Advances by <paramref name="bitCount"/> bits without reading.
    /// Used to skip spare/reserved bits within a fixed item.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitCount"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Too few bits remain.</exception>
    public void Skip(int bitCount)
    {
        if (bitCount < 0)
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Must be non-negative");
        if (bitCount > RemainingBits)
            throw new InvalidOperationException(
                $"Cannot skip {bitCount} bits: only {RemainingBits} remain at byte offset {ByteOffset}");
        _bitPosition += bitCount;
    }

    /// <summary>
    /// Advances to the next byte boundary. No-op if already aligned.
    /// Used after reading all named fields in a fixed item when spare bits trail.
    /// </summary>
    public void AlignToByte()
    {
        if (!IsAligned)
            _bitPosition = (_bitPosition + 7) & ~7; // round up to the nearest multiple of 8
    }

    /// <summary>
    /// Moves to an absolute bit position. Used by the SPF decoder to enforce
    /// length-bound blocks: after decoding all entries, seek to
    /// <c>blockStart + lengthField * 8</c> to guarantee alignment even in lenient mode.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Position is outside the buffer.</exception>
    public void SetPosition(int bitPosition)
    {
        if ((uint)bitPosition > (uint)(_data.Length << 3))
            throw new ArgumentOutOfRangeException(nameof(bitPosition),
                $"Cannot seek to bit {bitPosition}: buffer is {_data.Length * 8} bits");
        _bitPosition = bitPosition;
    }
}