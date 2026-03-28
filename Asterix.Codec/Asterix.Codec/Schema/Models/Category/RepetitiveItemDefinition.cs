namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An ASTERIX item that starts with an inline count field followed by N repetitions of
/// a fixed element structure.
///
/// <para>
/// The decoder reads <see cref="CountField"/> bits as an unsigned integer to determine
/// how many times to decode <see cref="Element"/>.
/// </para>
/// </summary>
public sealed class RepetitiveItemDefinition : ItemDefinition
{
    /// <summary>Defines the bit width of the leading count field.</summary>
    public CountFieldDefinition CountField { get; }

    /// <summary>The item definition decoded exactly Count times.</summary>
    public ItemDefinition Element { get; }

    public RepetitiveItemDefinition(CountFieldDefinition countField, ItemDefinition element)
    {
        CountField = countField;
        Element = element;
    }
}
