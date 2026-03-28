namespace Asterix.Codec.Exceptions;

/// <summary>
/// Thrown when a decoded model cannot be encoded back to binary.
/// </summary>
public sealed class EncodeException : AsterixCodecException
{
    /// <summary>
    /// Dot-path of the field being encoded when the error occurred.
    /// </summary>
    public string FieldPath { get; }

    public EncodeException(string fieldPath, string message)
        : base(string.IsNullOrEmpty(fieldPath) ? message : $"[path '{fieldPath}'] {message}")
    {
        FieldPath = fieldPath;
    }

    public EncodeException(string fieldPath, string message, Exception inner)
        : base(string.IsNullOrEmpty(fieldPath) ? message : $"[path '{fieldPath}'] {message}", inner)
    {
        FieldPath = fieldPath;
    }
}
