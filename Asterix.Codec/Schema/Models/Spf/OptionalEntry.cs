namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An SPF structure entry that is decoded only when a corresponding presence flag is set.
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
/// If true, <see cref="Field"/> is decoded and its value stored in context under
/// <see cref="SpfStructureEntry.Name"/>. If false, the field is skipped entirely.
/// </para>
///
/// <para>
/// Both <see cref="PresenceGroup"/> and <see cref="PresenceField"/> are validated at load time
/// by <c>SchemaValidator</c>: <see cref="PresenceGroup"/> must name a <see cref="DynamicPresenceEntry"/>
/// that appears earlier in the same structure, and <see cref="PresenceField"/> must be one of
/// its controlled field names.
/// </para>
/// </summary>
public sealed class OptionalEntry : SpfStructureEntry
{
    /// <summary>
    /// Name of the <see cref="DynamicPresenceEntry"/> holding the flag.
    /// </summary>
    public string PresenceGroup { get; }

    /// <summary>
    /// Name of the specific field within that presence group.
    /// </summary>
    public string PresenceField { get; }

    public FieldDefinition Field { get; }

    public OptionalEntry(string name, string presenceGroup, string presenceField, FieldDefinition field)
        : base(name)
    {
        PresenceGroup = presenceGroup;
        PresenceField = presenceField;
        Field = field;
    }
}
