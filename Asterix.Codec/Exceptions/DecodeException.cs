namespace Asterix.Codec.Exceptions;

/// <summary>
/// Thrown when a binary payload cannot be decoded according to its schema.
///
/// <para>
/// <see cref="ByteOffset"/> and <see cref="FieldPath"/> together give precise context:
/// the byte position in the original buffer where the error occurred, and the
/// dot-path of the field being decoded (e.g. <c>"I062_380.adr.address"</c>).
/// </para>
/// </summary>
public sealed class DecodeException : AsterixCodecException
{
    /// <summary>Zero-based byte offset in the original data buffer where decoding failed.</summary>
    public int ByteOffset { get; }

    /// <summary>
    /// Dot-path identifying the field being decoded when the error occurred.
    /// Empty string for packet-level errors (header, length).
    /// Examples: <c>"I062_010"</c>, <c>"I062_380.adr"</c>, <c>"I062_290[2].age"</c>
    /// </summary>
    public string FieldPath { get; }

    public DecodeException(int byteOffset, string fieldPath, string message)
        : base(FormatMessage(byteOffset, fieldPath, message))
    {
        ByteOffset = byteOffset;
        FieldPath = fieldPath;
    }

    public DecodeException(int byteOffset, string fieldPath, string message, Exception inner)
        : base(FormatMessage(byteOffset, fieldPath, message), inner)
    {
        ByteOffset = byteOffset;
        FieldPath = fieldPath;
    }

    private static string FormatMessage(int byteOffset, string fieldPath, string message) =>
        string.IsNullOrEmpty(fieldPath)
            ? $"[byte {byteOffset}] {message}"
            : $"[byte {byteOffset}, path '{fieldPath}'] {message}";
}
