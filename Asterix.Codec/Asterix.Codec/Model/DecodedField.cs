namespace Asterix.Codec.Model;

/// <summary>
/// The decoded value of a single bit-field within a fixed or SPF item.
///
/// <para>
/// Exactly one of <see cref="ScaledValue"/> or <see cref="StringValue"/> will be non-null
/// depending on the field type:
/// </para>
/// <list type="bullet">
///   <item><see cref="Schema.Models.FieldType.UInt"/> / <see cref="Schema.Models.FieldType.Int"/>:
///     <see cref="RawValue"/> holds the bit pattern; <see cref="ScaledValue"/> holds the
///     physical value (raw × scale), or equals <see cref="RawValue"/> cast to double if no scale.
///   </item>
///   <item><see cref="Schema.Models.FieldType.Bool"/>:
///     <see cref="RawValue"/> is 0 or 1; <see cref="ScaledValue"/> is null.
///   </item>
///   <item><see cref="Schema.Models.FieldType.String"/>:
///     <see cref="RawValue"/> is 0; <see cref="StringValue"/> holds the decoded string.
///   </item>
/// </list>
/// </summary>
public sealed class DecodedField
{
    public string Name { get; }

    /// <summary>
    /// Raw bit pattern as an unsigned integer. For signed fields (<see cref="Schema.Models.FieldType.Int"/>),
    /// this is the two's-complement bit pattern, not the sign-extended value.
    /// Use <see cref="ScaledValue"/> to get the signed physical value.
    /// </summary>
    public ulong RawValue { get; }

    /// <summary>
    /// Physical value after applying scale: <c>signedRaw × scale</c>.
    /// Null for Bool and String fields, and for numeric fields with no scale defined.
    /// </summary>
    public double? ScaledValue { get; }

    /// <summary>
    /// Decoded string. Non-null only for String fields.
    /// </summary>
    public string? StringValue { get; }

    public DecodedField(string name, ulong rawValue, double? scaledValue, string? stringValue)
    {
        Name = name;
        RawValue = rawValue;
        ScaledValue = scaledValue;
        StringValue = stringValue;
    }

    public override string ToString() =>
        StringValue is not null
            ? $"{Name}=\"{StringValue}\""
            : ScaledValue.HasValue
                ? $"{Name}={ScaledValue:G} (raw=0x{RawValue:X})"
                : $"{Name}=0x{RawValue:X}";
}
