namespace Asterix.Codec.Schema.Models;

/// <summary>
/// The top-level container for one or more SPF field set definitions loaded from a
/// single YAML file (e.g. <c>spf_custom_062.yml</c>).
///
/// <para>
/// A single YAML file may define multiple named field sets under the <c>spf_field_sets</c>
/// key. Each is independently validated and registered into <c>SchemaRegistry</c> by name.
/// </para>
/// </summary>
public sealed class SpfFieldSetSchema
{
    public int SchemaVersion { get; }

    /// <summary>
    /// All SPF field set definitions keyed by name (e.g. "SPF_CUSTOM_062").
    /// </summary>
    public IReadOnlyDictionary<string, SpfFieldSetDefinition> FieldSets { get; }

    public SpfFieldSetSchema(int schemaVersion, IReadOnlyDictionary<string, SpfFieldSetDefinition> fieldSets)
    {
        SchemaVersion = schemaVersion;
        FieldSets = fieldSets;
    }
}
