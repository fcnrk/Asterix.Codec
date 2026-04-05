# Changelog

---

## v0.1.0-alpha.1 — 2026-03-29

> **Alpha release — APIs may change without notice.**
> This is the first public preview of Asterix.Codec. Interfaces, type names, YAML schema keys, and
> exception contracts are all subject to breaking changes before a stable release.

### Overview

First end-to-end implementation of a schema-driven ASTERIX encode/decode library for .NET.
The library is fully YAML-driven: no category- or message-specific logic lives in the library
itself. All item structures, UAP ordering, scale factors, and SPF layouts are declared in YAML
files loaded at startup.

---

### What's included

#### Core decode / encode pipeline

- **`AsterixCodecBuilder` / `AsterixCodec`** — fluent builder produces an immutable, thread-safe
  codec. Schemas are validated eagerly at `Build()` time; malformed schemas throw before any
  decode or encode call is made.
- **`Decode`** — `AsterixCodec.Decode(byte[])` parses a full ASTERIX data block (3-byte header +
  records) into an `AsterixPacket` containing typed `DecodedRecord`s.
- **`Encode`** — `AsterixCodec.Encode(AsterixPacket)` reconstructs binary from the decoded model.
  The 3-byte header and all FSPEC bytes are computed automatically.
- **`RoundTrip`** — convenience method that decodes then re-encodes; output must be byte-for-byte
  identical to well-formed input.
- **Strict / Lenient decode modes** — `DecodeMode.Strict` throws `DecodeException` on any schema
  mismatch; `DecodeMode.Lenient` preserves unknown items as `UnknownDecodedItem` and clamps
  length overruns.

#### Supported item types

| YAML `type` | Wire format | Decoded as |
|---|---|---|
| `fixed` | Fixed-length, bit-packed fields | `FixedDecodedItem` |
| `compound` | Inner FSPEC + optional subitems | `CompoundDecodedItem` |
| `repetitive` | Count prefix + N × element | `RepetitiveDecodedItem` |
| `variable` | FX-extensible 8-bit groups | `VariableDecodedItem` |
| `explicit` (raw) | LEN prefix + raw content | `ExplicitDecodedItem` |
| `explicit` + structured schema | LEN prefix + sequentially decoded inner items | `StructuredExplicitDecodedItem` |

#### Field features

- `uint`, `int`, `bool`, `string` field types
- Scale factors (`scale: 0.25`, `scale: 1/128`, `scale: 360/65536`)
- String encodings: `ia5` (6-bit packed ICAO callsign), `ascii`
- Spare bits (explicit `bit:` offsets)

#### SPF (Special Purpose Field) support

- Separate `spf_custom_*.yml` schema files define SPF content independently of the category schema
- Decoder: `SpfDecoder.Decode` with a `SpfFieldSetDefinition`
- Encoder: `SpfEncoder.Encode`
- Supported SPF entry types: scalar, repetitive (with `count_ref`), dynamic presence flags,
  optional fields (with `present_if`)

#### Discriminated multi-message categories

- Categories can declare a `discriminator` block naming the fixed item and field that carry the
  message type value
- Each `MessageDefinition` in such a category carries a `discriminator` string; the decoder
  selects the correct UAP automatically at runtime using a two-phase FSPEC read
- Unknown discriminator values: throw `DecodeException` in Strict mode; fall back to the first
  message definition in Lenient mode

#### Structured-explicit items

- A `structured_explicit_cat*.yml` file describes the sequential inner schema of a
  `type: explicit` item
- Registered via `AsterixCodecBuilder.AddStructuredExplicitItemsFromYaml()`
- The registry substitutes the opaque `ExplicitItemDefinition` with a
  `StructuredExplicitItemDefinition` at `Build()` time (Freeze-time resolution)
- Inner items may be any supported type: `fixed`, `variable`, `repetitive`, `compound`
- Decoded as `StructuredExplicitDecodedItem` with a `Dictionary<string, DecodedItem>` keyed by
  inner-item ID

#### Schema validation

- All cross-references validated before the codec is returned: UAP item IDs, compound fspec keys,
  repetitive `count_ref`, SPF `present_if`, discriminator item and field, structured-explicit
  content IDs
- Detailed error context in all exceptions: source file path, category number, field path

#### Test coverage

230 tests across unit, integration, custom-schema, and negative test suites:

- Bit reader/writer, FSPEC parser/builder, string encoders, scale factors
- All item type decoders and encoders (including variable FX-bit chaining)
- SPF decode/encode round-trips
- CAT062 full decode and encode
- CAT253 discriminated category: schema loading, registry resolution, decode, encode, round-trip
- Structured-explicit item: all four inner item types, compound partial presence, round-trip
- Negative: invalid YAML, unknown categories, truncated packets, length mismatches

#### Sample project

`Asterix.Codec.Sample` — eight runnable demos covering every feature, from basic fixed-item
decode to a full CAT253 discriminated packet with a structured-explicit container.

---

### Known limitations

- **Single data block per call** — `Decode` and `Encode` operate on one ASTERIX data block at a
  time. Streaming / multi-block framing is not handled.
- **No RE (Reserved Expansion) item decoder** — RE items are treated identically to SP items
  (raw bytes). A dedicated schema type for RE may be added in a future release.
- **FX-bit implicit convention not validated** — Declaring the FX continuation bit as a named
  field in a `variable` item group silently produces a misaligned encode. The schema validator
  does not yet catch this.
- **No NuGet package published yet** — Reference the project directly or build from source.

---

### Dependencies

| Package | Version |
|---|---|
| `YamlDotNet` | 15.3.0 |
| `System.Memory` | 4.5.5 (netstandard2.0 only) |

**Targets:** `netstandard2.0` (.NET Framework 4.6.1+, Mono) and `net9.0`.
