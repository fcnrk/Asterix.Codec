using Asterix.Codec.Decode;
using Asterix.Codec.Encode;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;

namespace Asterix.Codec;

/// <summary>
/// Main entry point for ASTERIX encoding and decoding.
///
/// <para>
/// Create an instance via <see cref="AsterixCodecBuilder"/>:
/// <code>
///   AsterixCodec codec = new AsterixCodecBuilder()
///       .AddCategoryFromYaml("schemas/cat062.yml")
///       .AddSpfFieldSetFromYaml("schemas/spf_custom_062.yml")
///       .WithMode(DecodeMode.Strict)
///       .Build();
///
///   // Decode
///   AsterixPacket packet = codec.Decode(rawBytes);
///
///   // Encode
///   byte[] encoded = codec.Encode(packet);
///
///   // Round-trip verification
///   byte[] roundTripped = codec.RoundTrip(rawBytes);
/// </code>
/// </para>
///
/// <para>
/// Instances are immutable and thread-safe after construction.
/// </para>
/// </summary>
public sealed class AsterixCodec
{
    private readonly AsterixDecoder _decoder;
    private readonly AsterixEncoder _encoder;

    // Internal — callers use AsterixCodecBuilder.
    internal AsterixCodec(AsterixDecoder decoder, AsterixEncoder encoder)
    {
        _decoder = decoder;
        _encoder = encoder;
    }

    #region Decode

    /// <summary>
    /// Decodes a raw ASTERIX data block.
    /// </summary>
    /// <param name="data">
    ///   Complete ASTERIX data block: CAT (1 byte) + LEN (2 bytes big-endian) + records.
    /// </param>
    /// <returns>The decoded packet.</returns>
    /// <exception cref="DecodeException">
    ///   The data is malformed or references an item not defined in the schema
    ///   (strict mode only).
    /// </exception>
    public AsterixPacket Decode(byte[] data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        return _decoder.Decode(data);
    }

    /// <summary>
    /// Decodes a raw ASTERIX data block from a <see cref="ReadOnlySpan{T}"/>.
    /// Avoids an array allocation when the caller already holds a span.
    /// </summary>
    public AsterixPacket Decode(ReadOnlySpan<byte> data) => _decoder.Decode(data);

    #endregion

    #region Encode

    /// <summary>
    /// Encodes a decoded packet back to binary ASTERIX format.
    /// </summary>
    /// <param name="packet">The packet to encode.</param>
    /// <returns>
    ///   The encoded data block: CAT (1 byte) + LEN (2 bytes big-endian) + records.
    /// </returns>
    /// <exception cref="EncodeException">
    ///   A required field is missing or a schema mismatch is detected.
    /// </exception>
    public byte[] Encode(AsterixPacket packet)
    {
        if (packet is null) throw new ArgumentNullException(nameof(packet));
        return _encoder.Encode(packet);
    }

    #endregion

    #region Roundtrip

    /// <summary>
    /// Decodes <paramref name="data"/> and immediately re-encodes it.
    ///
    /// <para>
    /// Can be used for verifying round-trip correctness: the result should be
    /// byte-for-byte identical to <paramref name="data"/> for any well-formed payload
    /// that uses no unknown (lenient-only) items.
    /// </para>
    /// </summary>
    /// <param name="data">Raw ASTERIX data block.</param>
    /// <returns>Re-encoded bytes.</returns>
    public byte[] RoundTrip(byte[] data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        AsterixPacket packet = _decoder.Decode(data);
        return _encoder.Encode(packet);
    }

    /// <inheritdoc cref="RoundTrip(byte[])"/>
    public byte[] RoundTrip(ReadOnlySpan<byte> data)
    {
        AsterixPacket packet = _decoder.Decode(data);
        return _encoder.Encode(packet);
    }

    #endregion
}