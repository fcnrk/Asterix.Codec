# Design: `fspec_repetitive` Item Type

**Date:** 2026-04-04
**Status:** Approved

---

## Background

The Asterix.Codec DSL supports five item types for ASTERIX category schemas:

| Type | Count mechanism | Elements |
|---|---|---|
| `fixed` | N/A — single fixed block | N/A |
| `compound` | FSPEC bits (named, non-identical) | Each sub-item is distinct |
| `repetitive` | Explicit count field on the wire | N identical elements |
| `variable` | FX-bit chaining | Fixed groups |
| `explicit` | LEN byte | Opaque bytes |

A gap exists for the following ASTERIX structural pattern:

> **A list of N identical elements where N is determined by the FSPEC — each set FSPEC data bit corresponds to one element, with FX-bit extension for N > 7.**

This pattern is distinct from `compound` (which maps each FSPEC bit to a named, semantically distinct sub-item) and from `repetitive` (which reads an explicit count field from the wire before the elements).

---

## Motivating item: I062/510 — Composed Track Number

In ASTERIX CAT062 Edition 1.18, I062/510 lists the track numbers assigned by each contributing SDPS in a multi-SDPS fusion scenario. Each entry is 4 bytes: SAC (8 bits) + SIC (8 bits) + STN (16 bits). The number of entries is not a count prefix on the wire — it is encoded by which FSPEC bits are set.

This item cannot be correctly expressed with `compound` (which requires a distinct named sub-item per bit position) or `repetitive` (which requires an explicit count field).

---

## Gap analysis scope

The following categories were reviewed against the current DSL:

- **CAT062** (full ASTERIX Ed 1.18 standard)
- **CAT048** (full ASTERIX Ed 1.3/1.4 standard)
- **CAT253** (project sample)

### Items that fit existing types

All items in these categories fit existing types with the following notes:
- I048/100 (Mode C Code + Confidence): The 12 Gray-code bits contain a spare bit at position 10, splitting the code across two non-contiguous groups. Representable using explicit `bit:` offsets as two separate uint fields — awkward but not a structural gap.
- I048/250 (BDS Register Data): Standard `repetitive` item. BDS register interpretation is application-level; raw 56-bit fields suffice.
- I062/390 (Flight Plan Related Data): `compound` with a `repetitive` sub-item (TOD). Already supported — sub-items go through `MapItem` recursively.

### Item requiring a new type

| Item | Category | Gap |
|---|---|---|
| I062/510 — Composed Track Number | CAT062 | FSPEC-driven repetition of identical elements |

No other items in CAT062, CAT048, or CAT253 require a new type.

---

## New type: `fspec_repetitive`

### YAML syntax

```yaml
I062_510:
  type: fspec_repetitive
  element:
    type: fixed
    length: 4
    fields:
      - name: sac
        type: uint
        bits: 8
      - name: sic
        type: uint
        bits: 8
      - name: track_number
        type: uint
        bits: 16
```

`element` accepts any valid item definition (`fixed`, `compound`, `repetitive`, `variable`).

### Decode semantics

1. Read FSPEC bytes with FX-bit extension (identical to `compound` FSPEC reading).
2. Count the number of data bits set to 1 — call this N. (FX bits at position 7 of each FSPEC byte are excluded from the count.)
3. Decode N consecutive instances of `element` from the bit stream.
4. Return an ordered list of N decoded items.

### Encode semantics

1. Given a list of N elements, compute the number of FSPEC bytes needed: `ceil(N / 7)`.
2. Write FSPEC bytes: all data bits for positions 0..N-1 set to 1; FX bit set on all bytes except the last.
3. Encode each element in order.

### Round-trip correctness

Encoding always produces a FSPEC with exactly N bits set. Decoding that FSPEC counts N bits and decodes N elements. Round-trip is exact for any N ≥ 0.

### Edge cases

| Case | Behaviour |
|---|---|
| N = 0 | FSPEC is one byte, all bits 0. No elements decoded. |
| N = 7 | Single FSPEC byte, bits 7–1 all set, bit 0 (FX) = 0. |
| N = 8 | Two FSPEC bytes: first byte bits 7–1 set + FX=1; second byte bit 7 set + remaining bits 0, FX=0. |
| Strict mode: FSPEC bits set > elements on wire | Throw `DecodeException` with byte offset and field path. |
| Lenient mode: trailing bits with no data | Consume and ignore; return partial list. |

---

## Code changes

All changes are additive. No existing code is modified.

### Schema model layer

**New file:** `Asterix.Codec/Schema/Models/Category/FspecRepetitiveItemDefinition.cs`

```csharp
public sealed class FspecRepetitiveItemDefinition : ItemDefinition
{
    public ItemDefinition Element { get; }
    public FspecRepetitiveItemDefinition(ItemDefinition element) => Element = element;
}
```

### YAML loader

**File:** `Asterix.Codec/Schema/YamlSchemaLoader.cs`

Add one case to the `MapItem` switch:

```csharp
"fspec_repetitive" => new FspecRepetitiveItemDefinition(
    MapItem(dto.Element ?? throw new SchemaLoadException(hint,
        "FspecRepetitive item missing 'element'."), hint)),
```

No DTO changes are needed — `ItemDto.Element` already exists.

### Schema validator

**File:** `Asterix.Codec/Schema/SchemaValidator.cs`

Add a validation branch for `FspecRepetitiveItemDefinition` that recursively validates the `Element` definition. Mirror the existing `RepetitiveItemDefinition` validation path.

### Decoded model

**New file:** `Asterix.Codec/Model/FspecRepetitiveDecodedItem.cs`

A distinct type (rather than reusing `RepetitiveDecodedItem`) so consumers can distinguish the source of N and dispatch on concrete type:

```csharp
public sealed class FspecRepetitiveDecodedItem : DecodedItem
{
    public IReadOnlyList<DecodedItem> Elements { get; }
    public int Count => Elements.Count;
    public FspecRepetitiveDecodedItem(IReadOnlyList<DecodedItem> elements) => Elements = elements;
}
```

### Decoder

**New file:** `Asterix.Codec/Decode/ItemDecoders/FspecRepetitiveItemDecoder.cs`

```
1. Call FspecParser.Parse(ref reader) to read the FSPEC.
2. Count set data bits (bit positions 0..6 per byte; bit 7 = FX excluded).
3. Loop N times: call ItemDecoderDispatcher.Decode(ref reader, definition.Element, context).
4. Return FspecRepetitiveDecodedItem(elements).
```

**File:** `Asterix.Codec/Decode/ItemDecoders/ItemDecoderDispatcher.cs`

Add dispatch case for `FspecRepetitiveItemDefinition`.

### Encoder

**New file:** `Asterix.Codec/Encode/ItemEncoders/FspecRepetitiveItemEncoder.cs`

```
1. Assert item is FspecRepetitiveDecodedItem; extract element list.
2. Compute FSPEC bytes from element count N.
3. Write FSPEC bytes using FspecBuilder (or equivalent).
4. Loop over elements: call ItemEncoderDispatcher.Encode(writer, element, definition.Element).
```

**File:** `Asterix.Codec/Encode/ItemEncoders/ItemEncoderDispatcher.cs`

Add dispatch case for `FspecRepetitiveItemDefinition`.

---

## Testing requirements

### Unit tests

- FSPEC bit counting: N = 0, 1, 7, 8, 14, 15 (boundary conditions around FX bytes)
- Decode with N = 0 (empty FSPEC)
- Decode with N = 1, N = 7 (single FSPEC byte, all data bits set)
- Decode with N = 8 (two FSPEC bytes)
- Strict mode: FSPEC claims N but stream is too short → `DecodeException`
- Schema validation: missing `element` → `SchemaValidationException`

### Integration tests

- Load updated `samples/cat062.yml` with `I062_510` defined as `fspec_repetitive`
- Decode a binary payload containing I062/510 with 1, 2, and 3 entries
- Encode a `FspecRepetitiveDecodedItem` with N entries; verify wire bytes match expected FSPEC + elements
- Round-trip: decode → encode → decode; output identical to input

### Sample schema update

Update `samples/cat062.yml`: replace the current `compound` definition of `I062_510` (with `stn1`, `stn2`, `stn3` sub-items) with the new `fspec_repetitive` definition.

---

## Success criteria

1. `I062_510` defined in YAML as `fspec_repetitive` loads without error.
2. A payload with 1 contributing system produces a `FspecRepetitiveDecodedItem` with 1 element.
3. A payload with 8 contributing systems (requiring two FSPEC bytes) decodes correctly.
4. Encode → decode round-trip is byte-for-byte identical.
5. No changes to any existing item type decoders, encoders, or schema models.
