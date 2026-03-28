namespace Asterix.Codec.Binary;

/// <summary>
/// A forward-only, MSB-first bit writer with a growable internal buffer.
///
/// <para>
/// Mirrors <see cref="BitReader"/> in convention: bit 0 of a field is the most significant bit
/// of the first byte. Every <c>WriteBits</c> call places its MSB at the current position.
/// </para>
///
/// <para>
/// The internal <c>byte[]</c> is zero-initialized and grows by doubling. Because writes only
/// OR bits into the buffer, the zero-initialized state means spare bits remain zero without
/// explicit padding writes.
/// </para>
///
/// <para>
/// Call <see cref="ToSpan"/> for a zero-copy view of the written bytes (valid until the next
/// write that triggers a reallocation), or <see cref="ToArray"/> for a safe copy.
/// </para>
/// </summary>
public sealed class BitWriter
{
    private byte[] _buffer;
    private int _bitPosition; // absolute bit index; 0 = MSB of byte 0

    private const int DefaultCapacity = 256;

    public BitWriter(int initialCapacity = DefaultCapacity)
    {
        if (initialCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Must be at least 1");
        _buffer = new byte[initialCapacity]; // zero-initialized by the runtime
        _bitPosition = 0;
    }

    /// <summary>
    /// Absolute bit index from the start of the buffer.
    /// </summary>
    public int BitPosition => _bitPosition;

    /// <summary>
    /// Number of bytes written (including any partially written byte).
    /// </summary>
    public int ByteLength => (_bitPosition + 7) >> 3;

    /// <summary>
    /// True when the current position is on an exact byte boundary.
    /// </summary>
    public bool IsAligned => (_bitPosition & 7) == 0;

    private void EnsureCapacity(int additionalBits)
    {
        int requiredBytes = (_bitPosition + additionalBits + 7) >> 3;
        if (requiredBytes <= _buffer.Length)
            return;

        // Double the buffer until large enough.
        int newSize = _buffer.Length;
        do
        {
            newSize <<= 1;
        } while (newSize < requiredBytes);

        Array.Resize(ref _buffer, newSize); // preserves existing content; new bytes are zero
    }

    /// <summary>
    /// Writes the low <paramref name="count"/> bits of <paramref name="value"/>, MSB first.
    /// Bits above <paramref name="count"/> in <paramref name="value"/> are silently masked.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not in [1, 64].</exception>
    public void WriteBits(ulong value, int count)
    {
        if ((uint)(count - 1) > 63u)
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must be 1–64");

        // Mask to count bits. Special-case count==64: (1UL << 64) wraps to 1 in C#.
        if (count < 64)
            value &= (1UL << count) - 1;

        EnsureCapacity(count);

        int remaining = count;

        while (remaining > 0)
        {
            int byteIndex = _bitPosition >> 3;
            int bitInByte = _bitPosition & 7; // 0 = MSB of this byte
            int available = 8 - bitInByte; // free bits in current byte
            int take = remaining < available ? remaining : available;

            int valueShift = remaining - take;
            byte chunk = (byte)((value >> valueShift) & (ulong)((1 << take) - 1));

            int byteShift = available - take;
            _buffer[byteIndex] |= (byte)(chunk << byteShift);

            _bitPosition += take;
            remaining -= take;
        }
    }

    /// <summary>
    /// Writes <paramref name="value"/> as <paramref name="count"/> bits, MSB first.
    /// Negative values are stored in two's-complement; upper bits beyond
    /// <paramref name="count"/> are masked (matching <see cref="BitReader.ReadSignedBits"/>).
    /// </summary>
    public void WriteSignedBits(long value, int count) => WriteBits((ulong)value, count);

    /// <summary>Writes a single bit. Writes 1 if <paramref name="value"/> is <c>true</c>.</summary>
    public void WriteBool(bool value) => WriteBits(value ? 1UL : 0UL, 1);

    /// <summary>
    /// Copies <paramref name="data"/> into the buffer byte-for-byte and advances past it.
    /// Requires byte alignment; throws if not aligned.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not byte-aligned.</exception>
    public void WriteBytes(ReadOnlySpan<byte> data)
    {
        if (!IsAligned)
            throw new InvalidOperationException(
                $"WriteBytes requires byte alignment; bit offset in current byte is {_bitPosition & 7}");

        EnsureCapacity(data.Length << 3);
        data.CopyTo(_buffer.AsSpan(ByteLength));
        _bitPosition += data.Length << 3;
    }

    /// <summary>
    /// Advances to the next byte boundary by writing zero bits. No-op if already aligned.
    /// The zero bits are already in the buffer (zero-initialized); this only moves the position.
    /// </summary>
    public void AlignToByte()
    {
        if (!IsAligned)
            _bitPosition = (_bitPosition + 7) & ~7;
    }

    /// <summary>
    /// Returns a zero-copy view of the written bytes. Valid until the next operation that
    /// triggers a buffer reallocation. Prefer <see cref="ToArray"/> when storing the result.
    /// </summary>
    public ReadOnlySpan<byte> ToSpan() => _buffer.AsSpan(0, ByteLength);

    /// <summary>Returns a copy of the written bytes as a new heap array.</summary>
    public byte[] ToArray() => _buffer.AsSpan(0, ByteLength).ToArray();

    /// <summary>
    /// Resets position to zero and clears the written bytes. Retains the internal buffer
    /// to avoid reallocation on reuse.
    /// </summary>
    public void Reset()
    {
        _buffer.AsSpan(0, ByteLength).Clear();
        _bitPosition = 0;
    }
}