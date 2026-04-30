namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Represents a reserved UAP position that carries no data.
/// FSPEC bits at spare positions must be 0; the decoder skips them silently.
/// </summary>
public sealed class SpareItemDefinition : ItemDefinition { }
