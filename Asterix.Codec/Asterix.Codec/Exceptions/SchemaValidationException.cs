namespace Asterix.Codec.Exceptions;

/// <summary>
/// Thrown when a loaded schema fails cross-reference or semantic validation.
/// See <see cref="SchemaValidator"/> for the checks that produce this exception.
/// </summary>
public sealed class SchemaValidationException : AsterixCodecException
{
    /// <summary>
    /// Path of the YAML file containing the invalid schema.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Category number, if the error is in a category schema. Null for SPF schemas.
    /// </summary>
    public int? Category { get; }

    /// <summary>
    /// Dot-path within the schema identifying the invalid element.
    /// Example: <c>"items.I062_210.fspec[2]"</c>, <c>"SPF_CUSTOM_062.structure[5].present_if"</c>
    /// </summary>
    public string ItemPath { get; }

    public SchemaValidationException(string filePath, int? category, string itemPath, string message)
        : base(FormatMessage(filePath, category, itemPath, message))
    {
        FilePath = filePath;
        Category = category;
        ItemPath = itemPath;
    }

    private static string FormatMessage(string filePath, int? category, string itemPath, string message)
    {
        var location = category.HasValue
            ? $"{filePath}, CAT{category:D3}, {itemPath}"
            : $"{filePath}, {itemPath}";
        return $"[{location}] {message}";
    }
}
