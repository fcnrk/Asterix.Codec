namespace Asterix.Codec.Exceptions;

/// <summary>
/// Thrown when a YAML schema file declares a <c>schema_version</c> that the
/// loader does not support. Only version 1 is supported.
/// </summary>
public sealed class UnsupportedSchemaVersionException : AsterixCodecException
{
    public string FilePath { get; }
    public int Version { get; }

    public UnsupportedSchemaVersionException(string filePath, int version)
        : base($"[{filePath}] Unsupported schema version {version}; only version 1 is supported")
    {
        FilePath = filePath;
        Version = version;
    }
}
