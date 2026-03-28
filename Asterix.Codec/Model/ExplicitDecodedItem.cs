namespace Asterix.Codec.Model;

/// <summary>
/// A decoded explicit (RE / SP) ASTERIX item produced by <c>ExplicitItemDecoder</c>.
///
/// <para>
/// The content bytes are preserved verbatim; no field-level decoding is performed.
/// This guarantees that round-tripping an RE/SP field through decode → encode
/// produces byte-for-byte identical output even when the internal structure of the
/// expansion field is unknown to the codec.
/// </para>
///
/// <para>
/// <see cref="Content"/> contains the bytes <em>after</em> the length byte (i.e.
/// <c>LEN − 1</c> bytes). The length byte itself is not stored — the encoder
/// recomputes it as <c>Content.Length + 1</c>.
/// </para>
/// </summary>
public sealed class ExplicitDecodedItem : DecodedItem
{
    /// <summary>
    /// Raw content bytes. Length = wire LEN − 1.
    /// May be empty if LEN = 1 (length byte only, no content).
    /// </summary>
    public byte[] Content { get; }

    public ExplicitDecodedItem(byte[] content)
    {
        Content = content;
    }
}
