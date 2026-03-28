namespace Asterix.Codec.Schema.Yaml;

internal sealed class StructuredExplicitItemSetSchemaDto
{
    public int SchemaVersion { get; set; }
    public int Category { get; set; }
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Key = item ID (e.g. "I253_100"), value = seItem item DTO.
    /// </summary>
    public Dictionary<string, StructuredExplicitItemDto> Items { get; set; } = new Dictionary<string, StructuredExplicitItemDto>();
}

internal sealed class StructuredExplicitItemDto
{
    public string Description { get; set; } = "";
    /// <summary>
    /// Ordered list of inner items. Each entry reuses <see cref="ItemDto"/> (all existing
    /// type/length/fields/fspec/subitems/... properties) plus the <c>id</c> property added
    /// to <see cref="ItemDto"/> to name the inner item within the seItem container.
    /// </summary>
    public List<ItemDto> Content { get; set; } = new List<ItemDto>();
}
