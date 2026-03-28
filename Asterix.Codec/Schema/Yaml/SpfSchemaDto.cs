namespace Asterix.Codec.Schema.Yaml;

internal sealed class SpfFieldSetSchemaDto
{
    public int SchemaVersion { get; set; }
    public Dictionary<string, SpfFieldSetDto> SpfFieldSets { get; set; } = [];
}

internal sealed class SpfFieldSetDto
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public List<SpfStructureEntryDto> Structure { get; set; } = [];
}

internal sealed class SpfStructureEntryDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int? Bits { get; set; }
    public string? CountRef { get; set; }
    public SpfElementDto? Element { get; set; }
    public int? BitWidth { get; set; }
    public List<string>? Fields { get; set; }
    public string? PresentIf { get; set; }
    public SpfFieldDto? Field { get; set; }
}

internal sealed class SpfElementDto
{
    public string Type { get; set; } = "";
    public List<FieldDto> Fields { get; set; } = [];
}

internal sealed class SpfFieldDto
{
    public string Type { get; set; } = "";
    public int? Bits { get; set; }
    public string? Encoding { get; set; }
    public int? Length { get; set; }
}
