namespace Asterix.Codec.Schema.Models;

/// <summary>
/// The complete validated runtime schema for a single ASTERIX category.
///
/// <para>
/// All cross-references within this schema (UAP → items, compound fspec → subitems)
/// are guaranteed valid by <c>SchemaValidator</c> before this object is constructed.
/// Decoders may look up any item by name without checking for null.
/// </para>
///
/// <para>
/// A category may contain multiple <see cref="MessageDefinition"/>s (e.g. CAT253).
/// Single-message categories have exactly one entry with <c>Id = "default"</c>.
/// </para>
/// </summary>
public sealed class AsterixCategorySchema
{
    /// <summary>
    /// ASTERIX category number (e.g. 62 for CAT062).
    /// </summary>
    public int Category { get; }

    public string Name { get; }

    public int SchemaVersion { get; }

    /// <summary>
    /// All message definitions for this category.
    /// Discriminated categories select a message at decode time;
    /// non-discriminated categories always use the single default message.
    /// </summary>
    public IReadOnlyList<MessageDefinition> Messages { get; }

    /// <summary>
    /// All item definitions keyed by item ID (e.g. "I062_010").
    /// Validated: every ID referenced in any UAP or compound fspec exists here.
    /// </summary>
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }

    /// <summary>
    /// Non-null for multi-message (discriminated) categories such as CAT253.
    /// Identifies which fixed item and field carry the message-type value used to
    /// select the correct UAP at decode time.
    /// </summary>
    public CategoryDiscriminator? MessageDiscriminator { get; }

    public AsterixCategorySchema(
        int category,
        string name,
        int schemaVersion,
        IReadOnlyList<MessageDefinition> messages,
        IReadOnlyDictionary<string, ItemDefinition> items,
        CategoryDiscriminator? messageDiscriminator = null)
    {
        Category = category;
        Name = name;
        SchemaVersion = schemaVersion;
        Messages = messages;
        Items = items;
        MessageDiscriminator = messageDiscriminator;
    }

    /// <summary>
    /// Returns a copy of this schema with a substituted items dictionary.
    /// Used by <c>SchemaRegistry.Freeze()</c> to inject structured-explicit item definitions.
    /// </summary>
    internal AsterixCategorySchema WithItems(IReadOnlyDictionary<string, ItemDefinition> items)
        => new(Category, Name, SchemaVersion, Messages, items, MessageDiscriminator);
}
