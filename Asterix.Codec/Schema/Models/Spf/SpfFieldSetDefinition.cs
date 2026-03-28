namespace Asterix.Codec.Schema.Models;

/// <summary>
/// The complete validated runtime definition of a single SPF field set.
///
/// <para>
/// <see cref="Structure"/> defines the exact decode order, which is mandatory per CLAUDE.md:
/// length → count fields → repetitive blocks → dynamic presence → optional fields.
/// The decoder processes entries in list order without reordering.
/// </para>
///
/// <para>
/// All cross-references within <see cref="Structure"/> (count_ref, present_if) are
/// validated by <c>SchemaValidator</c> before this object is constructed:
/// forward references are rejected; every reference must name an earlier entry.
/// </para>
/// </summary>
public sealed class SpfFieldSetDefinition
{
    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// Ordered list of structure entries. Decoded strictly in this order.
    /// </summary>
    public IReadOnlyList<SpfStructureEntry> Structure { get; }

    public SpfFieldSetDefinition(string name, string description, IReadOnlyList<SpfStructureEntry> structure)
    {
        Name = name;
        Description = description;
        Structure = structure;
    }
}
