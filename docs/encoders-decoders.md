# Encoders and Decoders

This document describes the full decode and encode pipeline: entry points, record-level logic, item dispatch, and each specialized item decoder/encoder.

---

## Pipeline overview

```
Decode:
  byte[] / ReadOnlySpan<byte>
    └─► AsterixDecoder           header + record loop
          └─► RecordDecoder      FSPEC → item list → per-item dispatch
                └─► ItemDecoderDispatcher → FixedItemDecoder
                                          → CompoundItemDecoder
                                          → RepetitiveItemDecoder
                                          → FspecRepetitiveItemDecoder
                                          → VariableItemDecoder
                                          → ExplicitItemDecoder
                                          → StructuredExplicitItemDecoder

Encode:
  AsterixPacket
    └─► AsterixEncoder           header (computed after records)
          └─► RecordEncoder      FSPEC rebuild → UAP-ordered item encode
                └─► ItemEncoderDispatcher → FixedItemEncoder
                                          → CompoundItemEncoder
                                          → RepetitiveItemEncoder
                                          → FspecRepetitiveItemEncoder
                                          → VariableItemEncoder
                                          → ExplicitItemEncoder
                                          → StructuredExplicitItemEncoder
```

---

## Decode engine

### AsterixDecoder

**File:** `Asterix.Codec/Decode/AsterixDecoder.cs`

Entry point. Parses the 3-byte ASTERIX header, verifies the declared length matches the buffer, then loops over records until the buffer is consumed.

```
1. Read CAT (8 bits) → look up AsterixCategorySchema in registry
2. Read LEN (16 bits big-endian) → verify buffer length
3. While bytes remain after header: decode one record via RecordDecoder
4. Return AsterixPacket(category, records)
```

In **strict** mode, any leftover bytes after all records are decoded raise a `DecodeException`. In **lenient** mode, trailing bytes are silently ignored.

### RecordDecoder

**File:** `Asterix.Codec/Decode/RecordDecoder.cs`

Decodes one ASTERIX record.

```
1. Read FSPEC (FspecParser.ReadPresence)
2. If discriminated category:
   a. Decode the discriminator item (fixed item, first in UAP)
   b. Read discriminator field value
   c. Select MessageDefinition by discriminator value
3. Map presence array to item IDs via FspecParser.GetPresentItemIds and the UAP
4. For each present item ID:
   a. Look up ItemDefinition in category schema
   b. Call ItemDecoderDispatcher.Decode
5. Return DecodedRecord
```

Unknown item IDs (present in FSPEC but not in schema) throw in strict mode; in lenient mode they are skipped using the item's declared length.

### ItemDecoderDispatcher

**File:** `Asterix.Codec/Decode/ItemDecoders/ItemDecoderDispatcher.cs`

Static switch on the concrete `ItemDefinition` type. Returns a `DecodedItem`.

```csharp
internal static DecodedItem Decode(
    ref BitReader reader,
    ItemDefinition definition,
    string itemPath,
    DecodeMode mode)
=> definition switch
{
    FixedItemDefinition        fixed_   => FixedItemDecoder.Decode(ref reader, fixed_, itemPath),
    CompoundItemDefinition     compound => CompoundItemDecoder.Decode(ref reader, compound, itemPath, mode),
    RepetitiveItemDefinition   rep      => RepetitiveItemDecoder.Decode(ref reader, rep, itemPath, mode),
    FspecRepetitiveItemDefinition fspecRep => FspecRepetitiveItemDecoder.Decode(ref reader, fspecRep, itemPath, mode),
    VariableItemDefinition     variable => VariableItemDecoder.Decode(ref reader, variable, itemPath),
    ExplicitItemDefinition     _        => ExplicitItemDecoder.Decode(ref reader, itemPath),
    StructuredExplicitItemDefinition se => StructuredExplicitItemDecoder.Decode(ref reader, se, itemPath, mode),
    _ => throw new DecodeException(...)
};
```

The dispatcher cannot use virtual dispatch because `BitReader` is a `ref struct` and cannot be stored in interface method parameters without boxing.

---

## Item decoders

### FixedItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/FixedItemDecoder.cs`

Slices exactly `definition.Length` bytes from the reader, then decodes each `FieldDefinition` from that slice using `FieldDecoder`.

```
1. reader.Slice(definition.Length) → local reader scoped to item bytes
2. For each FieldDefinition:
   a. Seek to FieldDefinition.BitOffset within the slice
   b. Read FieldDefinition.Bits bits
   c. Decode value via FieldDecoder (uint/int/bool/string)
3. Return FixedDecodedItem(fields)
```

Using a scoped slice ensures that a misaligned field definition cannot corrupt the outer reader's position.

### FieldDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/FieldDecoder.cs`

Decodes a single field value given raw bits and a `FieldDefinition`.

| FieldType | Behaviour |
|---|---|
| `UInt` | Raw bits as `ulong`. If scale defined, compute `ScaledValue = raw × scale`. |
| `Int` | Sign-extend `Bits`-wide two's complement to `long`, then cast. `RawValue` holds unsigned bit pattern. |
| `Bool` | 1 bit → `RawValue` 0 or 1. |
| `String` | Delegate to `StringEncoders.DecodeIa5` or `DecodeAscii`. `StringValue` populated; `RawValue` is raw bit pattern. |

### CompoundItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/CompoundItemDecoder.cs`

```
1. FspecParser.ReadPresence → inner presence array
2. FspecParser.GetPresentItemIds(presence, definition.FspecOrder) → present subitem keys
3. For each present subitem key:
   a. Look up ItemDefinition in definition.Subitems
   b. ItemDecoderDispatcher.Decode → sub DecodedItem
4. Return CompoundDecodedItem(subitems)
```

The inner FSPEC uses the same FX-bit extension mechanism as the record-level FSPEC. Sub-items can be any type, including nested compound or repetitive items.

### RepetitiveItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/RepetitiveItemDecoder.cs`

```
1. Read definition.CountField.Bits bits → count N
2. Decode N elements via ItemDecoderDispatcher.Decode(definition.Element)
3. Return RepetitiveDecodedItem(elements)
```

The count field is an unsigned integer immediately preceding the element sequence on the wire.

### FspecRepetitiveItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/FspecRepetitiveItemDecoder.cs`

```
1. FspecParser.ReadPresence → inner presence array (FX-chained, same as compound)
2. Count set bits in presence array → N
   (Note: FspecParser caps at 16 bytes = 112 elements; accepted for fspec_repetitive
    because 112 contributing systems exceeds any realistic deployment)
3. Decode N consecutive elements via ItemDecoderDispatcher.Decode(definition.Element)
4. Return FspecRepetitiveDecodedItem(elements)
```

Unlike `compound`, all elements are identical in structure. Unlike `repetitive`, there is no explicit count field on the wire — N is derived solely from the FSPEC.

### VariableItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/VariableItemDecoder.cs`

```
1. While FX bit of last read byte = 1 (or first byte not yet read):
   a. Read 8 bits → raw byte
   b. Decode bits 7–1 against the current VariableGroupDefinition
   c. Advance to next group definition
2. Return VariableDecodedItem(groups)
```

Each byte contributes exactly 7 data bits. The FX bit (bit 0) controls continuation. Groups beyond the declared definition count (FX still set) are decoded into empty field lists in lenient mode; strict mode raises an exception.

### ExplicitItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/ExplicitItemDecoder.cs`

```
1. Read LEN byte → content length in bytes (includes the LEN byte itself)
2. Read (LEN - 1) bytes → raw content
3. Return ExplicitDecodedItem(content)
```

Content is preserved verbatim. No interpretation is applied.

### StructuredExplicitItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/StructuredExplicitItemDecoder.cs`

```
1. Read LEN byte → boundary
2. Slice exactly (LEN - 1) bytes into a local reader
3. For each StructuredExplicitContentEntry in definition.Content (in order):
   a. ItemDecoderDispatcher.Decode(ref localReader, entry.Definition, ...)
4. Return StructuredExplicitDecodedItem(items)
```

The local reader ensures decoding cannot escape the explicit item's boundary. Inner items can be any supported type: `fixed`, `compound`, `repetitive`, `variable`.

---

## Encode engine

### AsterixEncoder

**File:** `Asterix.Codec/Encode/AsterixEncoder.cs`

Entry point. Encodes each record, then prepends the 3-byte header with the computed total length.

```
1. For each DecodedRecord: RecordEncoder.Encode → bytes
2. Compute total LEN = 3 (header) + sum of record byte lengths
3. Write CAT (8 bits) + LEN (16 bits big-endian) + record bytes
```

LEN is computed after all records are encoded because record sizes are not known in advance.

### RecordEncoder

**File:** `Asterix.Codec/Encode/RecordEncoder.cs`

```
1. Determine which items are present: record.Items.Keys
2. FspecBuilder.WriteFspec(uap, presentItemIds, writer) → FSPEC bytes
3. For each item ID in UAP order:
   a. If present: ItemEncoderDispatcher.Encode
```

The FSPEC is always recomputed from the set of present item IDs. The decoded model does not store FSPEC bytes; this guarantees round-trip correctness even for programmatically-constructed records.

### ItemEncoderDispatcher

**File:** `Asterix.Codec/Encode/ItemEncoders/ItemEncoderDispatcher.cs`

Static switch on the `(DecodedItem, ItemDefinition)` pair. Both the item and the definition must match for encoding to proceed.

---

## Item encoders

### FixedItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/FixedItemEncoder.cs`

```
1. Create a local BitWriter scoped to definition.Length bytes
2. For each FieldDefinition:
   a. Seek to BitOffset
   b. Write field.RawValue into Bits bits via FieldEncoder
3. Zero-pad any spare bits (gaps between fields)
4. Write the local buffer to the outer writer
```

Spare bits — gaps at declared `bit:` offsets — are always written as zero.

### FieldEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/FieldEncoder.cs`

Always encodes `field.RawValue`. `ScaledValue` and `StringValue` are ignored. This is intentional: the raw bit pattern is the authoritative representation and is what guarantees byte-for-byte round-trips.

For string fields, `RawValue` holds the packed bit representation produced by `StringEncoders` at decode time, so no re-encoding is needed.

### CompoundItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/CompoundItemEncoder.cs`

```
1. Determine present subitem IDs from item.Subitems.Keys
2. FspecBuilder.WriteFspec(definition.FspecOrder, presentIds, writer) → inner FSPEC
3. For each subitem ID in definition.FspecOrder:
   a. If present: ItemEncoderDispatcher.Encode
```

The inner FSPEC is rebuilt the same way as the record-level FSPEC.

### RepetitiveItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/RepetitiveItemEncoder.cs`

```
1. Write item.Count as definition.CountField.Bits bits
2. For each element: ItemEncoderDispatcher.Encode(element, definition.Element)
```

### FspecRepetitiveItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/FspecRepetitiveItemEncoder.cs`

```
1. N = item.Elements.Count
2. Compute FSPEC byte count: N == 0 ? 1 : ceil(N / 7)
3. For each FSPEC byte:
   a. Set data bits MSB-first for elements in this byte's range
   b. Set FX = 1 on all bytes except the last
4. For each element: ItemEncoderDispatcher.Encode(element, definition.Element)
```

For N = 0 a single zero byte is written (valid FSPEC, FX = 0, no elements).

### VariableItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/VariableItemEncoder.cs`

```
1. For each group in item.Groups:
   a. Encode 7 data bits for the group's fields
   b. Write FX = 1 if more groups follow, FX = 0 on the last group
```

### ExplicitItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/ExplicitItemEncoder.cs`

```
1. Write LEN = item.Content.Length + 1 (length includes the LEN byte itself)
2. Write item.Content verbatim
```

### StructuredExplicitItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/StructuredExplicitItemEncoder.cs`

```
1. Encode inner items into a temporary buffer
2. Write LEN = buffer.Length + 1
3. Write the temporary buffer
```

LEN is computed after inner encoding because inner item sizes are not known in advance.

---

## DecodeMode

**File:** `Asterix.Codec/Decode/DecodeMode.cs`

```csharp
public enum DecodeMode { Strict, Lenient }
```

| Condition | Strict | Lenient |
|---|---|---|
| Unknown item ID in FSPEC | `DecodeException` | Skip (use declared length) |
| FSPEC bits beyond UAP length | `DecodeException` | Ignore |
| Buffer ends mid-field | `DecodeException` | Partial value or empty |
| Unknown discriminator value | `DecodeException` | Fall back to first message |
| Explicit/structured-explicit length mismatch | `DecodeException` | Clamp to available bytes |

---

## Error context

All decode exceptions carry:
- `ByteOffset` — absolute byte position in the input buffer where the failure occurred
- `FieldPath` — dot-notation path to the failing item/field (e.g. `"I062_380.adr.address"`)

All encode exceptions carry:
- `FieldPath` — dot-notation path to the failing item/field

Schema load and validation errors carry:
- `FilePath` — source YAML file path
- `Category` — category number if applicable
- `ItemPath` — schema path to the failing definition
