namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Describes a single bit-field within a fixed or SPF element.
///
/// <para>
/// <see cref="BitOffset"/> is the absolute offset from the MSB of the parent item (0 = MSB).
/// It is resolved by <c>YamlSchemaLoader</c> from either an explicit <c>bit:</c> declaration
/// or by accumulating widths of preceding fields. Spare bits (gaps between named fields)
/// are identified during loading and not represented here.
/// </para>
///
/// <para>
/// For <see cref="FieldType.String"/> fields, <see cref="Encoding"/> and <see cref="StringLength"/>
/// (in bytes) must be set. For <see cref="FieldType.UInt"/> or <see cref="FieldType.Int"/> fields,
/// <see cref="Scale"/> may be set. For <see cref="FieldType.Bool"/>, <see cref="Bits"/> is always 1.
/// </para>
/// </summary>
public sealed class FieldDefinition
{
    public string Name { get; }

    public FieldType Type { get; }

    /// <summary>
    /// Bit width of this field within the parent item.
    /// </summary>
    public int Bits { get; }

    /// <summary>
    /// Absolute bit offset from the MSB of the parent item (0 = MSB).
    /// Resolved at load time — decoders use this directly without recalculation.
    /// </summary>
    public int BitOffset { get; }

    /// <summary>
    /// Optional rational scale applied after raw extraction. Null means no scaling.
    /// </summary>
    public ScaleFactor? Scale { get; }

    /// <summary>
    /// String encoding. Set only when <see cref="Type"/> is <see cref="FieldType.String"/>.
    /// </summary>
    public StringEncoding? Encoding { get; }

    /// <summary>
    /// Byte length of the string. Set only when <see cref="Type"/> is <see cref="FieldType.String"/>.
    /// </summary>
    public int? StringLength { get; }

    public FieldDefinition(
        string name,
        FieldType type,
        int bits,
        int bitOffset,
        ScaleFactor? scale = null,
        StringEncoding? encoding = null,
        int? stringLength = null)
    {
        Name = name;
        Type = type;
        Bits = bits;
        BitOffset = bitOffset;
        Scale = scale;
        Encoding = encoding;
        StringLength = stringLength;
    }
}
