namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Represents a reserved UAP position that carries no data.
/// FSPEC bits at spare positions must always be 0.
/// </summary>
public sealed class SpareItemDefinition : ItemDefinition { }
