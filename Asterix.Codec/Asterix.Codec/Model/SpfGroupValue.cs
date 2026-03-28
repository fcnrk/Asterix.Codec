namespace Asterix.Codec.Model;

/// <summary>
/// One decoded element from a <c>SpfRepetitiveEntry</c>.
/// Each instance represents a single iteration of the group structure.
///
/// <para>Example: for the <c>f1</c> repetitive entry in <c>SPF_CUSTOM_062</c>,
/// each <see cref="SpfGroupValue"/> contains fields <c>f2</c> and <c>f3</c>.</para>
/// </summary>
public sealed class SpfGroupValue
{
    /// <summary>
    /// Fields in declaration order, matching <c>SpfElementDefinition.Fields</c>.
    /// </summary>
    public IReadOnlyList<DecodedField> Fields { get; }

    public SpfGroupValue(IReadOnlyList<DecodedField> fields) => Fields = fields;

    /// <summary>
    /// Returns the first field with <paramref name="name"/>, or <c>null</c>.
    /// </summary>
    public DecodedField? GetField(string name)
    {
        foreach (var f in Fields)
            if (f.Name == name) return f;
        return null;
    }
}
