namespace Asterix.Codec.Exceptions;

/// <summary>
/// Thrown when a YAML schema file cannot be read or parsed.
/// Covers file I/O failures and YAML syntax errors.
/// For semantic/cross-reference errors, see <see cref="SchemaValidationException"/>.
/// </summary>
public sealed class SchemaLoadException : AsterixCodecException
{
    /// <summary>
    /// Path of the YAML file that failed to load.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Line number within the YAML file, if available.
    /// </summary>
    public int? YamlLine { get; }

    public SchemaLoadException(string filePath, string message)
        : base($"[{filePath}] {message}")
    {
        FilePath = filePath;
    }

    public SchemaLoadException(string filePath, int yamlLine, string message)
        : base($"[{filePath}:{yamlLine}] {message}")
    {
        FilePath = filePath;
        YamlLine = yamlLine;
    }

    public SchemaLoadException(string filePath, string message, Exception inner)
        : base($"[{filePath}] {message}", inner)
    {
        FilePath = filePath;
    }
}
