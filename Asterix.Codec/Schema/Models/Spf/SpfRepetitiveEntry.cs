namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An SPF structure entry that decodes a group of fields N times, where N
/// was decoded earlier and stored in <c>DecodeContext</c>.
///
/// <para>
/// <see cref="CountRef"/> is the name of a previously decoded <see cref="ScalarEntry"/>
/// in the same SPF structure. The decoder resolves its value from <c>DecodeContext</c>
/// at decode time. This reference is validated at load time by <c>SchemaValidator</c>:
/// it must name a <see cref="ScalarEntry"/> that appears before this entry in
/// <see cref="SpfFieldSetDefinition.Structure"/>.
/// </para>
/// </summary>
public sealed class SpfRepetitiveEntry : SpfStructureEntry
{
    /// <summary>
    /// Name of the <see cref="ScalarEntry"/> in the same SPF structure whose decoded
    /// value determines how many times <see cref="Element"/> is decoded.
    /// </summary>
    public string CountRef { get; }

    public SpfElementDefinition Element { get; }

    public SpfRepetitiveEntry(string name, string countRef, SpfElementDefinition element) : base(name)
    {
        CountRef = countRef;
        Element = element;
    }
}
