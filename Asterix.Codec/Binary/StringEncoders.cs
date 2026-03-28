using System.Text;

namespace Asterix.Codec.Binary;

/// <summary>
/// Encode and decode string fields used in ASTERIX schemas.
///
/// Supported encodings:
/// <list type="bullet">
///   <item>
///     <b>IA5</b> — 6-bit packed encoding. Each character occupies exactly 6 bits (MSB-first).
///     Six bytes hold 8 characters. Used for callsigns (I062/245, I062/380.id).
///   </item>
///   <item>
///     <b>ASCII</b> — 8-bit per character, byte-for-byte. No packing. Trailing nulls and
///     spaces are stripped on decode.
///   </item>
/// </list>
/// </summary>
public static class StringEncoders
{
    #region IA5 Encoding

    private const int Ia5BitsPerChar = 6;

    /// <summary>
    /// Decodes a 6-bit-per-character IA5 string from <paramref name="data"/>.
    ///
    /// <para>
    /// Bit layout: characters are packed MSB-first, 6 bits each, with no padding between them.
    /// Six bytes yield 8 characters (<c>6 × 8 / 6 = 8</c>).
    /// </para>
    ///
    /// <para>
    /// 6-bit code → ASCII character mapping:
    /// <list type="bullet">
    ///   <item>codes 1–31 → <c>(char)(code | 0x40)</c> → 'A'–'_' region</item>
    ///   <item>codes 32–63 → <c>(char)code</c> → space, digits, punctuation</item>
    ///   <item>code 0 → treated as space (null-padded tail)</item>
    /// </list>
    /// Trailing spaces are trimmed from the result.
    /// </para>
    /// </summary>
    public static string DecodeIa5(ReadOnlySpan<byte> data)
    {
        int totalBits = data.Length * 8;
        int charCount = totalBits / Ia5BitsPerChar;

        if (charCount == 0)
            return string.Empty;

        Span<char> chars = charCount <= 64
            ? stackalloc char[charCount]
            : new char[charCount];

        int bitPos = 0;
        for (int i = 0; i < charCount; i++)
        {
            int code = ReadIa5Code(data, bitPos);
            chars[i] = Ia5CodeToChar(code);
            bitPos += Ia5BitsPerChar;
        }

        int length = charCount;
        while (length > 0 && chars[length - 1] == ' ')
            length--;

        return new string(chars.Slice(0, length).ToArray());
    }

    /// <summary>
    /// Encodes <paramref name="value"/> into <paramref name="writer"/> as 6-bit IA5 characters,
    /// padded with spaces to fill exactly <paramref name="byteLength"/> bytes.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is longer than the field's character capacity.
    /// </exception>
    public static void EncodeIa5(string value, int byteLength, BitWriter writer)
    {
        int maxChars = byteLength * 8 / Ia5BitsPerChar;

        if (value.Length > maxChars)
            throw new ArgumentException(
                $"String is {value.Length} characters; IA5 field capacity is {maxChars}", nameof(value));

        for (int i = 0; i < maxChars; i++)
        {
            char c = i < value.Length ? value[i] : ' ';
            writer.WriteBits((ulong)CharToIa5Code(c), Ia5BitsPerChar);
        }
    }

    #endregion

    #region ASCII Encoding

    /// <summary>
    /// Decodes a fixed-length ASCII byte field into a string.
    /// Trailing null bytes (<c>0x00</c>) and spaces are stripped.
    /// </summary>
    public static string DecodeAscii(ReadOnlySpan<byte> data)
    {
        int length = data.Length;
        while (length > 0 && (data[length - 1] == 0x00 || data[length - 1] == (byte)' '))
            length--;

#if NETSTANDARD2_0
        return length == 0 ? string.Empty : Encoding.ASCII.GetString(data.Slice(0, length).ToArray());
#else
        return length == 0 ? string.Empty : Encoding.ASCII.GetString(data[..length]);
#endif
    }

    /// <summary>
    /// Encodes <paramref name="value"/> into <paramref name="writer"/> as ASCII bytes,
    /// null-padded to <paramref name="byteLength"/> bytes.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is longer than <paramref name="byteLength"/>.
    /// </exception>
    public static void EncodeAscii(string value, int byteLength, BitWriter writer)
    {
        if (value.Length > byteLength)
            throw new ArgumentException(
                $"String is {value.Length} bytes; ASCII field capacity is {byteLength}", nameof(value));

        // Stack-allocate for typical short strings.
        Span<byte> bytes = byteLength <= 128
            ? stackalloc byte[byteLength]
            : new byte[byteLength];

        bytes.Clear(); // ensure null padding
#if NETSTANDARD2_0
        Encoding.ASCII.GetBytes(value).CopyTo(bytes);
#else
        Encoding.ASCII.GetBytes(value, bytes);
#endif
        writer.WriteBytes(bytes);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Reads a 6-bit IA5 code from <paramref name="data"/> at absolute bit <paramref name="bitPos"/>.
    /// </summary>
    private static int ReadIa5Code(ReadOnlySpan<byte> data, int bitPos)
    {
        int byteIndex = bitPos >> 3;
        int bitInByte = bitPos & 7; // 0 = MSB
        int available = 8 - bitInByte; // bits remaining in this byte

        if (available >= Ia5BitsPerChar)
        {
            // All 6 bits fit within one byte.
            int shift = available - Ia5BitsPerChar;
            return (data[byteIndex] >> shift) & 0x3F;
        }
        else
        {
            // 6 bits span two bytes.
            int bitsInFirst = available; 
            int bitsInSecond = Ia5BitsPerChar - bitsInFirst;

            int firstMask = (1 << bitsInFirst) - 1;
            int firstPart = data[byteIndex] & firstMask;

            int secondShift = 8 - bitsInSecond;
            int secondPart = (data[byteIndex + 1] >> secondShift) & ((1 << bitsInSecond) - 1);

            return (firstPart << bitsInSecond) | secondPart;
        }
    }

    /// <summary>
    /// Maps a 6-bit IA5 code to its ASCII character equivalent.
    ///
    /// Mapping:
    /// <list type="bullet">
    ///   <item>0     → ' ' (null padding treated as space)</item>
    ///   <item>1–31  → code | 0x40 (e.g. 1 → 'A', 26 → 'Z')</item>
    ///   <item>32–63 → (char)code  (e.g. 32 → ' ', 48 → '0', 57 → '9')</item>
    /// </list>
    /// </summary>
    private static char Ia5CodeToChar(int code) =>
        code switch
        {
            0 => ' ',
            <= 31 => (char)(code | 0x40),
            _ => (char)code
        };

    /// <summary>
    /// Maps an ASCII character to its 6-bit IA5 code.
    /// Only printable ASCII is supported; unsupported characters map to space (32).
    /// </summary>
    private static int CharToIa5Code(char c)
    {
        int ascii = c;
        if (ascii is >= 0x41 and <= 0x5F) return ascii & 0x3F; // A–Z, some symbols → 1–31
        if (ascii is >= 0x20 and <= 0x3F) return ascii; // space, digits, punct → 32–63
        if (ascii is >= 0x61 and <= 0x7A) return (ascii - 0x20) & 0x3F; // a–z → uppercase
        return 32; // fallback: space
    }

    #endregion
}