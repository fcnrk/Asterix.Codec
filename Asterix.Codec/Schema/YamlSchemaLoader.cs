using System.Globalization;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Schema.Models;
using Asterix.Codec.Schema.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Asterix.Codec.Schema;

/// <summary>
/// Loads and deserialises ASTERIX YAML schema files into validated runtime schema objects.
///
/// <para>
/// Supports two schema types:
/// </para>
/// <list type="bullet">
///   <item>
///     Category schemas (<c>cat*.yml</c>) — produces <see cref="AsterixCategorySchema"/>
///   </item>
///   <item>
///     SPF field set schemas (<c>spf_*.yml</c>) — produces <see cref="SpfFieldSetSchema"/>
///   </item>
/// </list>
///
/// <para>
/// Each load method validates the <c>schema_version</c> field before deserialisation.
/// Unsupported versions throw <see cref="UnsupportedSchemaVersionException"/> immediately.
/// Structural validation (cross-references, bit widths, UAP completeness) is delegated to
/// <see cref="SchemaValidator"/> and runs after deserialisation.
/// </para>
///
/// <para>
/// All overloads accept either a file path (string) or a <see cref="Stream"/>. The file-path
/// overloads open a <see cref="FileStream"/> and delegate to the stream overloads; they are
/// provided purely for convenience and do not add any validation logic.
/// </para>
/// </summary>
public static class YamlSchemaLoader
{
    private static IDeserializer CreateDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Loads a category schema from the file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path to a <c>cat*.yml</c> schema file.</param>
    /// <returns>A validated <see cref="AsterixCategorySchema"/>.</returns>
    /// <exception cref="UnsupportedSchemaVersionException">
    ///   The file declares an unsupported <c>schema_version</c>.
    /// </exception>
    /// <exception cref="SchemaValidationException">
    ///   The schema contains invalid cross-references or missing definitions.
    /// </exception>
    /// <exception cref="SchemaLoadException">
    ///   The file cannot be read or the YAML is structurally malformed.
    /// </exception>
    public static AsterixCategorySchema LoadCategory(string filePath)
    {
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        using var stream = File.OpenRead(filePath);
        return LoadCategory(stream, filePath);
    }

    /// <summary>
    /// Loads a category schema from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Readable stream containing YAML content.</param>
    /// <param name="sourceHint">
    ///   Optional label used in exception messages to identify the source
    ///   (e.g. the originating file path or embedded resource name).
    /// </param>
    public static AsterixCategorySchema LoadCategory(Stream stream, string? sourceHint = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        string hint = sourceHint ?? "<stream>";

        CategorySchemaDto dto;
        try
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
            dto = CreateDeserializer().Deserialize<CategorySchemaDto>(reader);
        }
        catch (YamlException ex)
        {
            throw new SchemaLoadException(hint,
                $"YAML parse error at line {ex.Start.Line}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not AsterixCodecException)
        {
            throw new SchemaLoadException(hint, $"Failed to read schema: {ex.Message}", ex);
        }

        if (dto is null)
            throw new SchemaLoadException(hint, "Schema file is empty or could not be parsed.");

        if (dto.SchemaVersion != 1)
            throw new UnsupportedSchemaVersionException(hint, dto.SchemaVersion);

        var items = dto.Items.ToDictionary(
            kv => kv.Key,
            kv => MapItem(kv.Value, hint),
            StringComparer.Ordinal);

        var messages = dto.Messages
            .Select(m => new MessageDefinition(m.Id, m.Name, m.Discriminator, m.Uap))
            .ToList();

        CategoryDiscriminator? disc = dto.Discriminator is { } d
            ? new CategoryDiscriminator(d.Item, d.Field)
            : null;

        var schema = new AsterixCategorySchema(
            dto.Category, dto.Name, dto.SchemaVersion, messages, items, disc);

        SchemaValidator.Validate(schema, hint);
        return schema;
    }


    #region Structured-explicit item set schema

    /// <summary>
    /// Loads a structured-explicit item set schema from the file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path to a <c>structured_explicit_cat*.yml</c> schema file.</param>
    public static StructuredExplicitItemSetSchema LoadStructuredExplicitItemSet(string filePath)
    {
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        using var stream = File.OpenRead(filePath);
        return LoadStructuredExplicitItemSet(stream, filePath);
    }

    /// <summary>
    /// Loads a structured-explicit item set schema from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Readable stream containing YAML content.</param>
    /// <param name="sourceHint">Optional label used in exception messages.</param>
    public static StructuredExplicitItemSetSchema LoadStructuredExplicitItemSet(Stream stream, string? sourceHint = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        string hint = sourceHint ?? "<stream>";

        StructuredExplicitItemSetSchemaDto dto;
        try
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
            dto = CreateDeserializer().Deserialize<StructuredExplicitItemSetSchemaDto>(reader);
        }
        catch (YamlException ex)
        {
            throw new SchemaLoadException(hint,
                $"YAML parse error at line {ex.Start.Line}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not AsterixCodecException)
        {
            throw new SchemaLoadException(hint, $"Failed to read schema: {ex.Message}", ex);
        }

        if (dto is null)
            throw new SchemaLoadException(hint, "Schema file is empty or could not be parsed.");

        if (dto.SchemaVersion != 1)
            throw new UnsupportedSchemaVersionException(hint, dto.SchemaVersion);

        var structuredExplicitItems = dto.Items.ToDictionary(
            kv => kv.Key,
            kv => MapStructuredExplicitItemDefinition(kv.Key, kv.Value, hint),
            StringComparer.Ordinal);

        var schema = new StructuredExplicitItemSetSchema(dto.SchemaVersion, dto.Category, dto.Name, structuredExplicitItems);
        SchemaValidator.Validate(schema, hint);
        return schema;
    }

    private static StructuredExplicitItemDefinition MapStructuredExplicitItemDefinition(
        string itemId,
        StructuredExplicitItemDto dto,
        string hint)
    {
        if (dto.Content == null || dto.Content.Count == 0)
            throw new SchemaLoadException(hint,
                $"Structured-explicit item '{itemId}' has an empty or missing 'content' list.");

        var content = dto.Content.Select(e =>
        {
            string id = e.Id ?? throw new SchemaLoadException(hint,
                $"Structured-explicit content entry in item '{itemId}' is missing required 'id'.");
            return new StructuredExplicitContentEntry(id, MapItem(e, hint));
        }).ToList();

        return new StructuredExplicitItemDefinition(content);
    }

    #endregion

    #region SPF field set schema

    /// <summary>
    /// Loads an SPF field set schema from the file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path to an <c>spf_*.yml</c> schema file.</param>
    /// <returns>A validated <see cref="SpfFieldSetSchema"/>.</returns>
    /// <exception cref="UnsupportedSchemaVersionException">
    ///   The file declares an unsupported <c>schema_version</c>.
    /// </exception>
    /// <exception cref="SchemaValidationException">
    ///   The schema contains invalid cross-references or structural errors.
    /// </exception>
    /// <exception cref="SchemaLoadException">
    ///   The file cannot be read or the YAML is structurally malformed.
    /// </exception>
    public static SpfFieldSetSchema LoadSpfFieldSet(string filePath)
    {
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        using var stream = File.OpenRead(filePath);
        return LoadSpfFieldSet(stream, filePath);
    }

    /// <summary>
    /// Loads an SPF field set schema from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Readable stream containing YAML content.</param>
    /// <param name="sourceHint">
    ///   Optional label used in exception messages to identify the source.
    /// </param>
    public static SpfFieldSetSchema LoadSpfFieldSet(Stream stream, string? sourceHint = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        string hint = sourceHint ?? "<stream>";

        SpfFieldSetSchemaDto dto;
        try
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
            dto = CreateDeserializer().Deserialize<SpfFieldSetSchemaDto>(reader);
        }
        catch (YamlException ex)
        {
            throw new SchemaLoadException(hint,
                $"YAML parse error at line {ex.Start.Line}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not AsterixCodecException)
        {
            throw new SchemaLoadException(hint, $"Failed to read schema: {ex.Message}", ex);
        }

        if (dto is null)
            throw new SchemaLoadException(hint, "Schema file is empty or could not be parsed.");

        if (dto.SchemaVersion != 1)
            throw new UnsupportedSchemaVersionException(hint, dto.SchemaVersion);

        var fieldSets = dto.SpfFieldSets.ToDictionary(
            kv => kv.Key,
            kv => MapSpfFieldSet(kv.Key, kv.Value, hint),
            StringComparer.Ordinal);

        var schema = new SpfFieldSetSchema(dto.SchemaVersion, fieldSets);
        SchemaValidator.Validate(schema, hint);
        return schema;
    }

    #endregion

    #region Mapping helpers

    private static ItemDefinition MapItem(ItemDto dto, string hint)
    {
        return dto.Type switch
        {
            "fixed" => new FixedItemDefinition(
                dto.Length ?? throw new SchemaLoadException(hint, "Fixed item missing 'length'."),
                MapFields(dto.Fields ?? [], hint)),

            "compound" => new CompoundItemDefinition(
                dto.Fspec ?? throw new SchemaLoadException(hint, "Compound item missing 'fspec'."),
                (dto.Subitems ?? throw new SchemaLoadException(hint, "Compound item missing 'subitems'."))
                .ToDictionary(kv => kv.Key, kv => MapItem(kv.Value, hint), StringComparer.Ordinal)),

            "repetitive" => new RepetitiveItemDefinition(
                new CountFieldDefinition(
                    (dto.CountField ?? throw new SchemaLoadException(hint, "Repetitive item missing 'count_field'."))
                    .Bits),
                MapItem(dto.Element ?? throw new SchemaLoadException(hint, "Repetitive item missing 'element'."),
                    hint)),

            "variable" => new VariableItemDefinition(
                (dto.Groups ?? throw new SchemaLoadException(hint, "Variable item missing 'groups'."))
                .Select(g => new VariableGroupDefinition(MapFields(g.Fields, hint)))
                .ToList()),

            "explicit" => new ExplicitItemDefinition(),

            _ => throw new SchemaLoadException(hint, $"Unknown item type '{dto.Type}'.")
        };
    }

    private static IReadOnlyList<FieldDefinition> MapFields(
        IEnumerable<FieldDto> dtos,
        string hint)
    {
        var result = new List<FieldDefinition>();
        int currentOffset = 0;

        foreach (var dto in dtos)
        {
            int bits = dto.Bits
                       ?? (dto.Type == "bool" ? 1
                           : dto.Type == "string" && dto.Length.HasValue ? dto.Length.Value * 8
                           : throw new SchemaLoadException(hint,
                               $"Field '{dto.Name}' is missing required 'bits' property."));

            int bitOffset;
            if (dto.Bit.HasValue)
            {
                bitOffset = dto.Bit.Value;
                currentOffset = bitOffset + bits;
            }
            else
            {
                bitOffset = currentOffset;
                currentOffset += bits;
            }

            result.Add(new FieldDefinition(
                dto.Name,
                MapFieldType(dto.Type, hint),
                bits,
                bitOffset,
                ParseScale(dto.Scale, hint),
                MapEncoding(dto.Encoding, hint),
                dto.Length));
        }

        return result;
    }

    private static FieldType MapFieldType(string type, string hint) => type switch
    {
        "uint" => FieldType.UInt,
        "int" => FieldType.Int,
        "bool" => FieldType.Bool,
        "string" => FieldType.String,
        _ => throw new SchemaLoadException(hint, $"Unknown field type '{type}'.")
    };

    private static StringEncoding? MapEncoding(string? encoding, string hint) => encoding switch
    {
        null => null,
        "ia5" => StringEncoding.Ia5,
        "ascii" => StringEncoding.Ascii,
        _ => throw new SchemaLoadException(hint, $"Unknown string encoding '{encoding}'.")
    };

    private static ScaleFactor? ParseScale(string? raw, string hint)
    {
        if (raw is null) return null;
        var s = raw.Trim();
        int slash = s.IndexOf('/');
        if (slash >= 0)
        {
            double n = double.Parse(s.Substring(0, slash).Trim(), CultureInfo.InvariantCulture);
            double d = double.Parse(s.Substring(slash + 1).Trim(), CultureInfo.InvariantCulture);
            return new ScaleFactor(n, d);
        }

        return ScaleFactor.FromDouble(double.Parse(s, CultureInfo.InvariantCulture));
    }

    private static SpfFieldSetDefinition MapSpfFieldSet(
        string name,
        SpfFieldSetDto dto,
        string hint)
    {
        var structure = dto.Structure
            .Select(e => MapSpfEntry(e, hint))
            .ToList();
        return new SpfFieldSetDefinition(name, dto.Description, structure);
    }

    private static SpfStructureEntry MapSpfEntry(SpfStructureEntryDto dto, string hint)
    {
        return dto.Type switch
        {
            "uint" => new ScalarEntry(dto.Name, FieldType.UInt,
                dto.Bits ?? throw new SchemaLoadException(hint,
                    $"SPF scalar entry '{dto.Name}' missing 'bits'.")),

            "int" => new ScalarEntry(dto.Name, FieldType.Int,
                dto.Bits ?? throw new SchemaLoadException(hint,
                    $"SPF scalar entry '{dto.Name}' missing 'bits'.")),

            "repetitive" => new SpfRepetitiveEntry(
                dto.Name,
                dto.CountRef ?? throw new SchemaLoadException(hint,
                    $"SPF repetitive entry '{dto.Name}' missing 'count_ref'."),
                MapSpfElement(
                    dto.Element ?? throw new SchemaLoadException(hint,
                        $"SPF repetitive entry '{dto.Name}' missing 'element'."),
                    hint)),

            "dynamic_presence" => new DynamicPresenceEntry(
                dto.Name,
                dto.BitWidth ?? throw new SchemaLoadException(hint,
                    $"SPF dynamic_presence entry '{dto.Name}' missing 'bit_width'."),
                dto.Fields ?? throw new SchemaLoadException(hint,
                    $"SPF dynamic_presence entry '{dto.Name}' missing 'fields'.")),

            "optional" => MapOptionalEntry(dto, hint),

            _ => throw new SchemaLoadException(hint,
                $"Unknown SPF structure entry type '{dto.Type}'.")
        };
    }

    private static OptionalEntry MapOptionalEntry(SpfStructureEntryDto dto, string hint)
    {
        string presentIf = dto.PresentIf ?? throw new SchemaLoadException(hint,
            $"SPF optional entry '{dto.Name}' missing 'present_if'.");
        var fieldDto = dto.Field ?? throw new SchemaLoadException(hint,
            $"SPF optional entry '{dto.Name}' missing 'field'.");

        int dotIdx = presentIf.IndexOf('.');
        if (dotIdx < 0)
            throw new SchemaLoadException(hint,
                $"SPF optional entry '{dto.Name}' present_if='{presentIf}' must be in 'group.field' format.");

        string presenceGroup = presentIf.Substring(0, dotIdx);
        string presenceField = presentIf.Substring(dotIdx + 1);

        var field = MapSpfField(dto.Name, fieldDto, hint);
        return new OptionalEntry(dto.Name, presenceGroup, presenceField, field);
    }

    private static SpfElementDefinition MapSpfElement(SpfElementDto dto, string hint)
    {
        var fields = MapFields(dto.Fields, hint);
        return new SpfElementDefinition(fields);
    }

    private static FieldDefinition MapSpfField(string entryName, SpfFieldDto dto, string hint)
    {
        FieldType fieldType = MapFieldType(dto.Type, hint);
        StringEncoding? encoding = MapEncoding(dto.Encoding, hint);

        int bits = dto.Bits ?? (fieldType == FieldType.String && dto.Length.HasValue
            ? dto.Length.Value * 8
            : throw new SchemaLoadException(hint,
                $"SPF optional field '{entryName}' missing 'bits'."));

        return new FieldDefinition(entryName, fieldType, bits, bitOffset: 0,
            encoding: encoding, stringLength: dto.Length);
    }

    #endregion
}