namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Defines a single message variant within a category.
///
/// <para>
/// Most categories have a single default message. CAT253 and similar use
/// <see cref="Discriminator"/> to select among multiple message definitions
/// at decode time.
/// </para>
///
/// <para>
/// <see cref="Uap"/> is the User Application Profile — the ordered list of item IDs
/// that maps FSPEC bit positions to item names. Index 0 = first data bit of first FSPEC byte.
/// Every ID in <see cref="Uap"/> is guaranteed by <c>SchemaValidator</c> to exist in the
/// parent <see cref="AsterixCategorySchema.Items"/> dictionary.
/// </para>
/// </summary>
public sealed class MessageDefinition
{
    public string Id { get; }
    public string Name { get; }

    /// <summary>
    /// Optional field value used to discriminate between multiple message types
    /// within a category. Null for single-message categories.
    /// </summary>
    public string? Discriminator { get; }

    /// <summary>
    /// Ordered item IDs defining the UAP. Maps FSPEC bit positions to item names.
    /// </summary>
    public IReadOnlyList<string> Uap { get; }

    public MessageDefinition(string id, string name, string? discriminator, IReadOnlyList<string> uap)
    {
        Id = id;
        Name = name;
        Discriminator = discriminator;
        Uap = uap;
    }
}
