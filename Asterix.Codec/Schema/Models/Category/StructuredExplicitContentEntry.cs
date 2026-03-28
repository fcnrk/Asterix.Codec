namespace Asterix.Codec.Schema.Models;

/// <summary>
/// A named inner-item slot within a <see cref="StructuredExplicitItemDefinition"/>.
/// Wraps an (Id, ItemDefinition) pair; a class is used instead of a value tuple
/// to avoid <c>IReadOnlyList&lt;T&gt;</c> portability issues on netstandard2.0.
/// </summary>
public sealed class StructuredExplicitContentEntry
{
    /// <summary>
    /// Logical identifier for this inner item (e.g. "position", "nav_data").
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Schema definition governing how this inner item is decoded and encoded.
    /// </summary>
    public ItemDefinition Definition { get; }

    public StructuredExplicitContentEntry(string id, ItemDefinition definition)
    {
        Id = id;
        Definition = definition;
    }
}
