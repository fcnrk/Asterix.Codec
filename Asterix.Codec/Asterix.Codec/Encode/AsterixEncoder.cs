using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode;

/// <summary>
/// Entry point for encoding <see cref="AsterixPacket"/> objects to binary ASTERIX data blocks.
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
internal sealed class AsterixEncoder
{
    private readonly SchemaRegistry _registry;

    internal AsterixEncoder(SchemaRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    #region Public API

    /// <summary>
    /// Encodes <paramref name="packet"/> to a binary ASTERIX data block.
    /// </summary>
    /// <returns>The encoded bytes, including the 3-byte CAT+LEN header.</returns>
    /// <exception cref="EncodeException">Any required field is missing or a schema mismatch is detected.</exception>
    public byte[] Encode(AsterixPacket packet)
    {
        if (!_registry.TryGetCategory(packet.Category, out AsterixCategorySchema? schema) || schema is null)
            throw new EncodeException(string.Empty,
                $"No schema registered for CAT{packet.Category:D3}");

        bool isDiscriminated = schema.MessageDiscriminator != null;

        var recordsWriter = new BitWriter();

        foreach (DecodedRecord record in packet.Records)
        {
            MessageDefinition message = isDiscriminated
                ? SelectMessage(record, schema)
                : schema.Messages[0];

            RecordEncoder.Encode(recordsWriter, record, schema, message);
        }

        int totalLength = 3 + recordsWriter.ByteLength;

        var output = new BitWriter(totalLength);
        output.WriteBits((ulong)packet.Category, 8); // CAT
        output.WriteBits((ulong)totalLength, 16); // LEN (big-endian)
        output.WriteBytes(recordsWriter.ToSpan()); // records

        return output.ToArray();
    }

    /// <inheritdoc cref="Encode(AsterixPacket)"/>
    public ReadOnlySpan<byte> EncodeAsSpan(AsterixPacket packet) => Encode(packet).AsSpan();

    #endregion

    #region Helpers

    private static MessageDefinition SelectMessage(DecodedRecord record, AsterixCategorySchema schema)
    {
        var disc = schema.MessageDiscriminator!;

        if (!record.Items.TryGetValue(disc.ItemId, out var item) || item is not FixedDecodedItem fixedItem)
            throw new EncodeException(disc.ItemId,
                $"Discriminator item '{disc.ItemId}' missing or not a fixed item " +
                $"in record for CAT{schema.Category:D3}");

        DecodedField? field = null;
        for (int i = 0; i < fixedItem.Fields.Count; i++)
        {
            if (fixedItem.Fields[i].Name == disc.FieldName)
            {
                field = fixedItem.Fields[i];
                break;
            }
        }

        if (field is null)
            throw new EncodeException(disc.ItemId,
                $"Discriminator field '{disc.FieldName}' not found in item '{disc.ItemId}'");

        string value = field.RawValue.ToString();

        foreach (var message in schema.Messages)
        {
            if (message.Discriminator == value)
                return message;
        }

        throw new EncodeException(disc.ItemId,
            $"No message definition for discriminator value '{value}' in CAT{schema.Category:D3}");
    }

    #endregion
}