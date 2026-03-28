using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode;

/// <summary>
/// Entry point for decoding ASTERIX binary data blocks.
///
/// <para>
/// ASTERIX data block structure:
/// <code>
///   byte 0    : CAT — category number
///   bytes 1–2 : LEN — total length in bytes (big-endian), includes the 3-byte header
///   bytes 3+  : one or more concatenated records
/// </code>
/// </para>
///
/// <para>
/// Thread-safe after construction: holds no mutable state.
/// </para>
/// </summary>
internal sealed class AsterixDecoder
{
    private readonly SchemaRegistry _registry;
    private readonly DecodeMode _mode;

    internal AsterixDecoder(SchemaRegistry registry, DecodeMode mode = DecodeMode.Strict)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _mode = mode;
    }

    #region Public API

    /// <summary>
    /// Decodes an ASTERIX data block from <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Raw bytes of a complete ASTERIX data block.</param>
    /// <returns>Decoded packet with all records.</returns>
    /// <exception cref="DecodeException">
    /// The data is malformed, or the schema is unknown and <see cref="DecodeMode.Strict"/> is active.
    /// </exception>
    public AsterixPacket Decode(ReadOnlySpan<byte> data) => DecodeInternal(data);

    /// <inheritdoc cref="Decode(ReadOnlySpan{byte})"/>
    public AsterixPacket Decode(byte[] data) => DecodeInternal(data.AsSpan());
    
    #endregion

    #region Private helpers

    private AsterixPacket DecodeInternal(ReadOnlySpan<byte> data)
    {
        if (data.Length < 3)
            throw new DecodeException(0, string.Empty,
                $"Data block too short: {data.Length} byte(s); minimum is 3 (CAT + LEN)");

        var headerReader = new BitReader(data);
        int category = (int)headerReader.ReadBits(8);   // byte 0: CAT
        int length   = (int)headerReader.ReadBits(16);  // bytes 1–2: LEN (big-endian)

        if (length < 3)
            throw new DecodeException(1, string.Empty,
                $"Invalid LEN field: {length}; minimum value is 3");

        if (length > data.Length)
            throw new DecodeException(1, string.Empty,
                $"LEN field ({length}) exceeds available data ({data.Length} bytes)");

        if (!_registry.TryGetCategory(category, out AsterixCategorySchema? schema) || schema is null)
        {
            if (_mode == DecodeMode.Strict)
                throw new DecodeException(0, string.Empty,
                    $"No schema registered for CAT{category:D3}");

            return new AsterixPacket(category, Array.Empty<DecodedRecord>());
        }

        ReadOnlySpan<byte> recordBytes = data.Slice(3, length - 3);
        var recordReader = new BitReader(recordBytes);

        bool isDiscriminated = schema.MessageDiscriminator != null;

        var records = new List<DecodedRecord>();

        while (recordReader.RemainingBits > 0)
        {
            if (recordReader.RemainingBits < 8)
            {
                if (_mode == DecodeMode.Strict)
                    throw new DecodeException(3 + recordReader.ByteOffset, string.Empty,
                        $"Unexpected {recordReader.RemainingBits} trailing bit(s) at end of data block");
                break;
            }

            DecodedRecord record = isDiscriminated
                ? RecordDecoder.DecodeDiscriminated(ref recordReader, schema, _mode)
                : RecordDecoder.Decode(ref recordReader, schema, schema.Messages[0], _mode);

            records.Add(record);
        }

        return new AsterixPacket(category, records);
    }
    #endregion
}
