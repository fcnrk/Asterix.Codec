namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An SPF structure entry that is decoded only when a corresponding presence flag is set,
/// containing a multi-field group instead of a single field.
///
/// <para>
/// The presence condition is a two-part reference split at load time from the YAML
/// <c>present_if</c> dot-path (e.g. <c>"presence.f4"</c> → <see cref="PresenceGroup"/> = "presence",
/// <see cref="PresenceField"/> = "f4"). Splitting at load time avoids string parsing at
/// decode time.
/// </para>
///
/// <para>
/// At decode time, the decoder evaluates: <c>DecodeContext["&lt;PresenceGroup&gt;.&lt;PresenceField&gt;"] != 0</c>.
/// If true, all fields in <see cref="Fields"/> are decoded in sequence and grouped into a
/// <see cref="SpfGroupValue"/>, which is stored in context under <see cref="SpfStructureEntry.Name"/>.
/// If false, all fields are skipped entirely and no value is stored.
/// </para>
///
/// <para>
/// Both <see cref="PresenceGroup"/> and <see cref="PresenceField"/> are validated at load time
/// by <c>SchemaValidator</c>: <see cref="PresenceGroup"/> must name a <see cref="DynamicPresenceEntry"/>
/// that appears earlier in the same structure, and <see cref="PresenceField"/> must be one of
/// its controlled field names.
/// </para>
/// </summary>
public sealed class OptionalGroupEntry : SpfStructureEntry
{
    /// <summary>
    /// Name of the <see cref="DynamicPresenceEntry"/> holding the flag.
    /// </summary>
    public string PresenceGroup { get; }

    /// <summary>
    /// Name of the specific field within that presence group.
    /// </summary>
    public string PresenceField { get; }

    /// <summary>
    /// The fields that make up this optional group, decoded in order when the presence flag is set.
    /// </summary>
    public IReadOnlyList<FieldDefinition> Fields { get; }

    /// <summary>
    /// Pre-constructed SPF element definition from <see cref="Fields"/>, avoiding per-call allocation.
    /// </summary>
    public SpfElementDefinition Element { get; }

    public OptionalGroupEntry(string name, string presenceGroup, string presenceField, IReadOnlyList<FieldDefinition> fields)
        : base(name)
    {
        PresenceGroup = presenceGroup;
        PresenceField = presenceField;
        Fields = fields;
        Element = new SpfElementDefinition(fields);
    }
}
