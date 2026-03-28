namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An explicitly length-prefixed ASTERIX data item whose content is treated as an
/// opaque byte block (RE — Reserved Expansion field, SP — Special Purpose field).
///
/// <para>
/// Wire format:
/// <code>
///   byte 0     : LEN — total length in bytes, including this byte
///   bytes 1..N : content  (N = LEN − 1 bytes)
/// </code>
/// </para>
///
/// <para>
/// The content is preserved verbatim as a <c>byte[]</c> in
/// <see cref="Model.ExplicitDecodedItem.Content"/>. No field-level decoding is
/// performed. This allows the codec to carry RE/SP payloads through a round-trip
/// without understanding their internal structure.
/// </para>
/// </summary>
public sealed class ExplicitItemDefinition : ItemDefinition { }
