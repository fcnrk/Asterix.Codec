using Asterix.Codec.Decode;
using Asterix.Codec.Encode;
using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec;

/// <summary>
/// Fluent builder for constructing an <see cref="AsterixCodec"/> instance.
///
/// <para>
/// Typical usage:
/// <code>
///   var codec = new AsterixCodecBuilder()
///       .AddCategoryFromYaml("schemas/cat062.yml")
///       .AddSpfFieldSetFromYaml("schemas/spf_custom_062.yml")
///       .WithMode(DecodeMode.Strict)
///       .Build();
/// </code>
/// </para>
///
/// <para>
/// <see cref="Build"/> validates that at least one schema has been registered and then
/// freezes the <see cref="SchemaRegistry"/> so no further mutation is possible.
/// The resulting <see cref="AsterixCodec"/> is immutable and thread-safe.
/// </para>
///
/// <para>
/// Schema loading errors (malformed YAML, invalid cross-references, unsupported versions)
/// are thrown eagerly from the <c>Add*</c> methods, not deferred to <see cref="Build"/>.
/// This gives the earliest possible feedback at startup.
/// </para>
/// </summary>
public sealed class AsterixCodecBuilder
{
    private readonly SchemaRegistry _registry = new();
    private DecodeMode _mode = DecodeMode.Strict;
    private int _schemaCount;
    private bool _built;

    #region Schema registration

    /// <summary>
    /// Loads and registers a category schema from the YAML file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path to a <c>cat*.yml</c> file.</param>
    /// <returns>This builder (fluent).</returns>
    /// <exception cref="Exceptions.SchemaLoadException">The file is unreadable or malformed.</exception>
    /// <exception cref="Exceptions.SchemaValidationException">The schema has invalid references.</exception>
    /// <exception cref="Exceptions.UnsupportedSchemaVersionException">The schema version is not supported.</exception>
    public AsterixCodecBuilder AddCategoryFromYaml(string filePath)
    {
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        AsterixCategorySchema schema = YamlSchemaLoader.LoadCategory(filePath);
        _registry.RegisterCategory(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Loads and registers a category schema from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Readable stream containing YAML content.</param>
    /// <param name="sourceHint">
    ///   Label used in diagnostics to identify the source (e.g. a resource name).
    /// </param>
    /// <returns>This builder (fluent).</returns>
    public AsterixCodecBuilder AddCategoryFromYaml(Stream stream, string? sourceHint = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        AsterixCategorySchema schema = YamlSchemaLoader.LoadCategory(stream, sourceHint);
        _registry.RegisterCategory(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Loads and registers an SPF field set schema from the YAML file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path to an <c>spf_*.yml</c> file.</param>
    /// <returns>This builder (fluent).</returns>
    /// <exception cref="Exceptions.SchemaLoadException">The file is unreadable or malformed.</exception>
    /// <exception cref="Exceptions.SchemaValidationException">The schema has invalid references.</exception>
    /// <exception cref="Exceptions.UnsupportedSchemaVersionException">The schema version is not supported.</exception>
    public AsterixCodecBuilder AddSpfFieldSetFromYaml(string filePath)
    {
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        SpfFieldSetSchema schema = YamlSchemaLoader.LoadSpfFieldSet(filePath);
        _registry.RegisterSpfFieldSets(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Loads and registers an SPF field set schema from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Readable stream containing YAML content.</param>
    /// <param name="sourceHint">
    ///   Label used in diagnostics to identify the source.
    /// </param>
    /// <returns>This builder (fluent).</returns>
    public AsterixCodecBuilder AddSpfFieldSetFromYaml(Stream stream, string? sourceHint = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        SpfFieldSetSchema schema = YamlSchemaLoader.LoadSpfFieldSet(stream, sourceHint);
        _registry.RegisterSpfFieldSets(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Registers a pre-loaded <see cref="AsterixCategorySchema"/> directly, bypassing YAML loading.
    /// Useful in tests and for programmatically constructed schemas.
    /// </summary>
    /// <returns>This builder (fluent).</returns>
    public AsterixCodecBuilder AddCategory(AsterixCategorySchema schema)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        _registry.RegisterCategory(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Registers a pre-loaded <see cref="SpfFieldSetSchema"/> directly, bypassing YAML loading.
    /// Useful in tests and for programmatically constructed schemas.
    /// </summary>
    /// <returns>This builder (fluent).</returns>
    public AsterixCodecBuilder AddSpfFieldSet(SpfFieldSetSchema schema)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        _registry.RegisterSpfFieldSets(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Loads and registers structured-explicit item definitions from the YAML file at <paramref name="filePath"/>.
    /// The file defines the inner structure of one or more <c>type: explicit</c> items in a category schema.
    /// At <c>Build()</c> time those explicit items are substituted with the structured-explicit definitions.
    /// </summary>
    /// <param name="filePath">Path to a <c>structured_explicit_cat*.yml</c> file.</param>
    /// <returns>This builder (fluent).</returns>
    /// <exception cref="Exceptions.SchemaLoadException">The file is unreadable or malformed.</exception>
    /// <exception cref="Exceptions.SchemaValidationException">The schema has invalid structure.</exception>
    /// <exception cref="Exceptions.UnsupportedSchemaVersionException">The schema version is not supported.</exception>
    public AsterixCodecBuilder AddStructuredExplicitItemsFromYaml(string filePath)
    {
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        StructuredExplicitItemSetSchema schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(filePath);
        _registry.RegisterStructuredExplicitItemSet(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Loads and registers structured-explicit item definitions from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Readable stream containing YAML content.</param>
    /// <param name="sourceHint">Label used in diagnostics to identify the source.</param>
    /// <returns>This builder (fluent).</returns>
    public AsterixCodecBuilder AddStructuredExplicitItemsFromYaml(Stream stream, string? sourceHint = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        StructuredExplicitItemSetSchema schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(stream, sourceHint);
        _registry.RegisterStructuredExplicitItemSet(schema);
        _schemaCount++;
        return this;
    }

    /// <summary>
    /// Registers a pre-loaded <see cref="StructuredExplicitItemSetSchema"/> directly, bypassing YAML loading.
    /// Useful in tests.
    /// </summary>
    /// <returns>This builder (fluent).</returns>
    public AsterixCodecBuilder AddStructuredExplicitItemSet(StructuredExplicitItemSetSchema schema)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        _registry.RegisterStructuredExplicitItemSet(schema);
        _schemaCount++;
        return this;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Sets the decode mode. Defaults to <see cref="DecodeMode.Strict"/>.
    ///
    /// <list type="bullet">
    ///   <item><see cref="DecodeMode.Strict"/> — any schema violation throws <see cref="Exceptions.DecodeException"/>.</item>
    ///   <item><see cref="DecodeMode.Lenient"/> — unknown items preserved; length overruns clamped silently.</item>
    /// </list>
    /// </summary>
    /// <returns>This builder (fluent).</returns>
    public AsterixCodecBuilder WithMode(DecodeMode mode)
    {
        _mode = mode;
        return this;
    }

    #endregion

    #region Build

    /// <summary>
    /// Builds and returns a thread-safe <see cref="AsterixCodec"/> instance.
    ///
    /// <para>
    /// Freezes the internal <see cref="SchemaRegistry"/> — no further schema registration
    /// is possible on this builder after <see cref="Build"/> is called.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">No schemas were registered.</exception>
    public AsterixCodec Build()
    {
        if (_built)
            throw new InvalidOperationException(
                "Build() has already been called on this builder. Create a new AsterixCodecBuilder to build another codec.");

        if (_schemaCount == 0)
            throw new InvalidOperationException(
                "No schemas have been registered. Call AddCategoryFromYaml or AddSpfFieldSetFromYaml before Build().");

        _built = true;
        _registry.Freeze();

        return new AsterixCodec(
            new AsterixDecoder(_registry, _mode),
            new AsterixEncoder(_registry));
    }

    #endregion
}