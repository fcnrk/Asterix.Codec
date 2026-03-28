namespace Asterix.Codec.Schema.Models;

/// <summary>
/// Describes the inline count field that precedes a repetitive item's elements.
/// The decoder reads this many bits as an unsigned integer to determine repetition count.
/// </summary>
public readonly record struct CountFieldDefinition(int Bits);
