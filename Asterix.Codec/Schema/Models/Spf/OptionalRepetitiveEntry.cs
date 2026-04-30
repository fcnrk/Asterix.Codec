namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An SPF structure entry that decodes a group of fields N times only when a presence flag is set,
/// where N is an implicit uint8 counter immediately preceding the repetitive data.
///
/// <para>
/// Unlike <see cref="SpfRepetitiveEntry"/>, which requires a reference to a previously decoded
/// <see cref="ScalarEntry"/> for its count, <see cref="OptionalRepetitiveEntry"/> uses an
/// inline uint8 count that is read immediately before the first repetition.
/// </para>
///
/// <para>
/// The presence condition is a two-part reference split at load time from the YAML
/// <c>present_if</c> dot-path (e.g. <c>"presence.f5"</c> → <see cref="PresenceGroup"/> = "presence",
/// <see cref="PresenceField"/> = "f5"). Splitting at load time avoids string parsing at
/// decode time.
/// </para>
///
/// <para>
/// At decode time, the decoder evaluates: <c>DecodeContext["&lt;PresenceGroup&gt;.&lt;PresenceField&gt;"] != 0</c>.
/// If true, it reads a uint8 count, then decodes <see cref="Element"/> that many times,
/// storing all elements in a <see cref="SpfOptionalRepetitiveValue"/> under
/// <see cref="SpfStructureEntry.Name"/>. If false, the entire repetitive block is skipped.
/// </para>
///
/// <para>
/// <see cref="PresenceGroup"/> and <see cref="PresenceField"/> are validated at load time:
/// <see cref="PresenceGroup"/> must name a <see cref="DynamicPresenceEntry"/> that appears
/// earlier in the same structure, and <see cref="PresenceField"/> must be one of its
/// controlled field names.
/// </para>
/// </summary>
public sealed class OptionalRepetitiveEntry : SpfStructureEntry
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
    /// The element definition that is repeated when the presence flag is set.
    /// The repetition count is read inline as a uint8.
    /// </summary>
    public SpfElementDefinition Element { get; }

    public OptionalRepetitiveEntry(string name, string presenceGroup, string presenceField, SpfElementDefinition element)
        : base(name)
    {
        PresenceGroup = presenceGroup;
        PresenceField = presenceField;
        Element = element;
    }
}
