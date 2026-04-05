# Asterix.Codec Documentation

A schema-driven .NET library for encoding and decoding Eurocontrol ASTERIX messages.

---

## Documents

| Document | Contents |
|---|---|
| [Bit Operations](bit-operations.md) | `BitReader`, `BitWriter`, `StringEncoders`, `FspecParser`, `FspecBuilder` |
| [Models](models.md) | Decoded item hierarchy, `DecodedField`, schema model types |
| [Encoders and Decoders](encoders-decoders.md) | Full decode/encode pipeline, all item decoders and encoders |
| [SPF](spf.md) | Special Purpose Field structure, YAML format, decode/encode |
| [Structured Explicit Items](structured-explicit.md) | Inner structure for RE/SP fields, freeze-time substitution |

---

## Quick start

```csharp
AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml("schemas/cat062.yml")
    .AddSpfFieldSetFromYaml("schemas/spf_custom_062.yml")
    .WithMode(DecodeMode.Strict)
    .Build();

AsterixPacket packet = codec.Decode(bytes);
DecodedRecord record  = packet.Records[0];

if (record.TryGet("I062_010", out var item) && item is FixedDecodedItem dsi)
    Console.WriteLine(dsi.GetField("sac")?.RawValue);

byte[] reencoded = codec.Encode(packet);
```

---

## Key design principles

- **Schema-driven:** all category, message, and SPF structure is declared in YAML. No category-specific code exists in the library.
- **Round-trip correctness:** FSPEC bytes are always recomputed; raw field values are always preserved; encode(decode(x)) == x.
- **Strict and lenient modes:** strict mode fails fast on any schema mismatch; lenient mode skips unknown items and clamps length overruns.
- **Strong typing:** each item type has a distinct `DecodedItem` subclass. Consumers pattern-match on concrete types.
- **No virtual dispatch on hot paths:** `BitReader` is a `ref struct`; item decoders use static pattern-matching switches.
