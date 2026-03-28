namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Base class for all entries in an SPF field set structure.
///
/// <para>
/// SPF structure entries are decoded in strict sequential order (see CLAUDE.md).
/// The concrete subtype determines the decode strategy:
/// </para>
/// <list type="bullet">
///   <item><see cref="ScalarEntry"/> — fixed-width unsigned or signed value</item>
///   <item><see cref="SpfRepetitiveEntry"/> — repeated group, count resolved from context</item>
///   <item><see cref="DynamicPresenceEntry"/> — reads one flag per named field into the presence map</item>
///   <item><see cref="OptionalEntry"/> — conditionally decoded based on a presence flag</item>
/// </list>
/// </summary>
public abstract class SpfStructureEntry
{
    public string Name { get; }

    protected SpfStructureEntry(string name) => Name = name;
}
