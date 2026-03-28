namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Defines the fields of a single element within a <see cref="SpfRepetitiveEntry"/>.
///
/// <para>
/// Fields are decoded sequentially (MSB-first), with each field's
/// <see cref="FieldDefinition.BitOffset"/> pre-resolved at load time.
/// </para>
/// </summary>
public sealed class SpfElementDefinition
{
    public IReadOnlyList<FieldDefinition> Fields { get; }

    public SpfElementDefinition(IReadOnlyList<FieldDefinition> fields) => Fields = fields;
}
