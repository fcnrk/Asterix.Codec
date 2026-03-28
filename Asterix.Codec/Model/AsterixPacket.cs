namespace Asterix.Codec.Model;

/// <summary>
/// A decoded ASTERIX data block: one category, one or more records.
///
/// <para>
/// An ASTERIX data block has the structure:
/// <code>
///   byte 0:     category (CAT)
///   bytes 1–2:  total length in bytes (big-endian, includes the 3-byte header)
///   bytes 3+:   N consecutive records
/// </code>
/// Each record is independently decoded; they all share the same category schema.
/// </para>
/// </summary>
public sealed class AsterixPacket
{
    /// <summary>
    /// ASTERIX category number (e.g. 62 for CAT062).
    /// </summary>
    public int Category { get; }

    /// <summary>
    /// All decoded records from this data block, in order.
    /// </summary>
    public IReadOnlyList<DecodedRecord> Records { get; }

    public AsterixPacket(int category, IReadOnlyList<DecodedRecord> records)
    {
        Category = category;
        Records = records;
    }
}
