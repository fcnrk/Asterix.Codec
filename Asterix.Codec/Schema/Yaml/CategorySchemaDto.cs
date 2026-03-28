namespace Asterix.Codec.Schema.Yaml;

internal sealed class CategorySchemaDto
{
    public int SchemaVersion { get; set; }
    public int Category { get; set; }
    public string Name { get; set; } = "";
    public CategoryDiscriminatorDto? Discriminator { get; set; }
    public List<MessageDto> Messages { get; set; } = [];
    public Dictionary<string, ItemDto> Items { get; set; } = [];
}

internal sealed class CategoryDiscriminatorDto
{
    public string Item { get; set; } = "";
    public string Field { get; set; } = "";
}

internal sealed class MessageDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Discriminator { get; set; }
    public List<string> Uap { get; set; } = [];
}

internal sealed class ItemDto
{
    /// <summary>
    /// Inner-item identifier used when this ItemDto appears inside a seItem content list.
    /// Not used when ItemDto is a value in the top-level <c>items</c> dictionary
    /// (the map key is the ID in that case).
    /// </summary>
    public string? Id { get; set; }

    public string Type { get; set; } = "";
    public int? Length { get; set; }
    public List<FieldDto>? Fields { get; set; }
    public List<string>? Fspec { get; set; }
    public Dictionary<string, ItemDto>? Subitems { get; set; }
    public CountFieldDto? CountField { get; set; }
    public ItemDto? Element { get; set; }
    public List<VariableGroupDto>? Groups { get; set; }
}

internal sealed class FieldDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int? Bits { get; set; }
    public int? Bit { get; set; }
    public string? Scale { get; set; }
    public string? Encoding { get; set; }
    public int? Length { get; set; }
}

internal sealed class CountFieldDto
{
    public int Bits { get; set; }
}

internal sealed class VariableGroupDto
{
    public List<FieldDto> Fields { get; set; } = [];
}
