namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An ASTERIX structured-explicit application data item: a 1-byte length prefix (total field
/// size including the length byte itself) followed by inner items decoded sequentially.
/// Each inner item is identified by a <see cref="StructuredExplicitContentEntry.Id"/>.
///
/// <para>
/// Instances of this class are created at <c>SchemaRegistry.Freeze()</c> time when a
/// <see cref="StructuredExplicitItemSetSchema"/> is registered for a category that has a matching
/// <c>type: explicit</c> item. The <c>ExplicitItemDefinition</c> in the category schema is
/// substituted with this richer definition so that decoder dispatch routes to
/// <c>StructuredExplicitItemDecoder</c> automatically.
/// </para>
/// </summary>
public sealed class StructuredExplicitItemDefinition : ItemDefinition
{
    /// <summary>
    /// Ordered list of inner-item slots. Decoded sequentially; no FSPEC.
    /// </summary>
    public IReadOnlyList<StructuredExplicitContentEntry> Content { get; }

    public StructuredExplicitItemDefinition(IReadOnlyList<StructuredExplicitContentEntry> content)
    {
        Content = content;
    }
}
