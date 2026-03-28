namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Top-level container for one or more structured-explicit item definitions loaded from a single
/// <c>structured_explicit_cat*.yml</c> file.
///
/// <para>
/// Analogous to <see cref="SpfFieldSetSchema"/>: one file per category, keyed by item ID
/// (e.g. "I253_100"). Registered via <c>AsterixCodecBuilder.AddStructuredExplicitItemsFromYaml</c>.
/// </para>
///
/// <para>
/// At <c>SchemaRegistry.Freeze()</c> time the registry substitutes each matching
/// <c>ExplicitItemDefinition</c> in the category schema with the corresponding
/// <see cref="StructuredExplicitItemDefinition"/>, so that decoder dispatch works without any
/// additional runtime context.
/// </para>
/// </summary>
public sealed class StructuredExplicitItemSetSchema
{
    public int SchemaVersion { get; }
    public int Category { get; }
    public string Name { get; }

    /// <summary>
    /// Item ID → StructuredExplicitItemDefinition (e.g. "I253_100" → definition with 4 inner items).
    /// </summary>
    public IReadOnlyDictionary<string, StructuredExplicitItemDefinition> Items { get; }

    public StructuredExplicitItemSetSchema(
        int schemaVersion,
        int category,
        string name,
        IReadOnlyDictionary<string, StructuredExplicitItemDefinition> items)
    {
        SchemaVersion = schemaVersion;
        Category = category;
        Name = name;
        Items = items;
    }
}
