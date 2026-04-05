# Decoded Model

This document describes the strongly-typed model that the decode engine produces and the encode engine consumes. All types live in the `Asterix.Codec.Model` namespace.

---

## Overview

Decoding an ASTERIX binary buffer produces an `AsterixPacket`. Encoding takes an `AsterixPacket` and produces a binary buffer. The model is the shared contract between the two.

```
byte[] ──► AsterixDecoder ──► AsterixPacket ──► AsterixEncoder ──► byte[]
```

The model is **immutable**: all collections are read-only and all types are sealed. Consumers navigate the hierarchy by pattern-matching on concrete `DecodedItem` subtypes.

---

## AsterixPacket

**File:** `Asterix.Codec/Model/AsterixPacket.cs`

The top-level container for a decoded ASTERIX data block.

```csharp
public sealed class AsterixPacket
{
    public int Category { get; }
    public IReadOnlyList<DecodedRecord> Records { get; }
}
```

One data block can contain multiple records of the same category.

---

## DecodedRecord

**File:** `Asterix.Codec/Model/DecodedRecord.cs`

One ASTERIX record: a dictionary of item IDs to decoded items.

```csharp
public sealed class DecodedRecord
{
    public IReadOnlyDictionary<string, DecodedItem> Items { get; }
    public bool TryGet(string itemId, out DecodedItem? item);
}
```

Item IDs match the keys defined in the YAML schema (e.g. `"I062_010"`, `"SP"`). Items not present in the wire encoding are absent from the dictionary; the dictionary is never populated with null values.

---

## DecodedItem hierarchy

`DecodedItem` is the abstract base class for all item types. Consumers use pattern matching:

```csharp
if (record.TryGet("I062_210", out var item) && item is CompoundDecodedItem compound)
    // ...
```

### FixedDecodedItem

**File:** `Asterix.Codec/Model/FixedDecodedItem.cs`

A fixed-length item decoded into named bit fields.

```csharp
public sealed class FixedDecodedItem : DecodedItem
{
    public IReadOnlyList<DecodedField> Fields { get; }
    public DecodedField? GetField(string name);
}
```

Fields appear in declaration order (MSB first). Each field carries a raw integer value and, if a scale factor is defined in the schema, a pre-computed scaled floating-point value.

### CompoundDecodedItem

**File:** `Asterix.Codec/Model/CompoundDecodedItem.cs`

A compound item whose sub-items are selected by an inner FSPEC. Only sub-items that were present on the wire appear in `Subitems`.

```csharp
public sealed class CompoundDecodedItem : DecodedItem
{
    public IReadOnlyDictionary<string, DecodedItem> Subitems { get; }
    public bool TryGet(string subitemId, out DecodedItem? item);
}
```

Sub-item keys are the names declared in the YAML `fspec:` list (e.g. `"qx"`, `"adr"`). Each value is itself a `DecodedItem` of whatever type the sub-item's schema declares.

### RepetitiveDecodedItem

**File:** `Asterix.Codec/Model/RepetitiveDecodedItem.cs`

A repetitive item where element count is given by an explicit count field on the wire.

```csharp
public sealed class RepetitiveDecodedItem : DecodedItem
{
    public IReadOnlyList<DecodedItem> Elements { get; }
    public int Count { get; }
}
```

All elements have identical structure (defined by the schema's `element:` block).

### FspecRepetitiveDecodedItem

**File:** `Asterix.Codec/Model/FspecRepetitiveDecodedItem.cs`

A repetitive item where element count is determined by counting set data bits in an inner FSPEC (no explicit count field on the wire). Used for items like I062/510 (Composed Track Number) where N identical elements are present and N is FSPEC-encoded.

```csharp
public sealed class FspecRepetitiveDecodedItem : DecodedItem
{
    public IReadOnlyList<DecodedItem> Elements { get; }
    public int Count { get; }
}
```

Distinct from `RepetitiveDecodedItem` so that consumers can pattern-match on the concrete type and understand the source of N.

### VariableDecodedItem

**File:** `Asterix.Codec/Model/VariableDecodedItem.cs`

A variable-length item made up of FX-bit-chained byte groups. Each group contributes 7 data bits and one FX bit. Groups are decoded in declaration order; the number of groups decoded equals the number of FX-chained bytes on the wire.

```csharp
public sealed class VariableDecodedItem : DecodedItem
{
    public IReadOnlyList<IReadOnlyList<DecodedField>> Groups { get; }
    public DecodedField? GetField(string name);  // searches all groups
}
```

### ExplicitDecodedItem

**File:** `Asterix.Codec/Model/ExplicitDecodedItem.cs`

An opaque explicit item (SP or RE field). The content is preserved verbatim as raw bytes.

```csharp
public sealed class ExplicitDecodedItem : DecodedItem
{
    public byte[] Content { get; }
}
```

The length prefix byte is not included in `Content` — it is stripped by the decoder and rebuilt by the encoder.

### StructuredExplicitDecodedItem

**File:** `Asterix.Codec/Model/StructuredExplicitDecodedItem.cs`

An explicit item whose inner structure is known and decoded via a separately-loaded schema. The length prefix byte still governs the boundary but the content is decoded into named inner items.

```csharp
public sealed class StructuredExplicitDecodedItem : DecodedItem
{
    public IReadOnlyDictionary<string, DecodedItem> Items { get; }
}
```

Inner item keys are the IDs declared in the structured-explicit schema file.

### SpfDecodedItem

**File:** `Asterix.Codec/Model/SpfDecodedItem.cs`

The result of decoding a Special Purpose Field block using a `SpfFieldSetDefinition`. The internal structure follows the SPF schema order: length, count fields, repetitive blocks, dynamic presence flags, conditional fields.

```csharp
public sealed class SpfDecodedItem : DecodedItem
{
    public IReadOnlyDictionary<string, object?> Fields { get; }

    public ulong?                               GetScalar(string name);
    public IReadOnlyList<SpfGroupValue>?        GetRepetitive(string name);
    public IReadOnlyDictionary<string, ulong>?  GetPresenceFlags(string name);
    public DecodedField?                        GetOptional(string name);
}
```

`Fields` is a heterogeneous dictionary; the accessor methods provide typed access without casting.

---

## DecodedField

**File:** `Asterix.Codec/Model/DecodedField.cs`

A single named bit field within a `FixedDecodedItem` or `VariableDecodedItem` group.

```csharp
public sealed class DecodedField
{
    public string  Name        { get; }
    public ulong   RawValue    { get; }
    public double? ScaledValue { get; }
    public string? StringValue { get; }
}
```

- `RawValue` is always populated. It is the unsigned bit pattern extracted from the wire, cast to `ulong`. For signed integer fields the two's-complement interpretation is the consumer's responsibility (raw bits are preserved as-is).
- `ScaledValue` is populated when the schema declares a `scale:` factor. Value = `RawValue × scale`.
- `StringValue` is populated for `string` fields (IA5 or ASCII). `RawValue` is the raw bit pattern; `StringValue` is the decoded text.

**Round-trip rule:** encoders always write `RawValue`, never `ScaledValue` or `StringValue`. This guarantees that encode(decode(x)) = x regardless of floating-point precision.

---

## SpfGroupValue

**File:** `Asterix.Codec/Model/SpfGroupValue.cs`

One element from an SPF repetitive block. Holds the fields decoded for a single repetition.

```csharp
public sealed class SpfGroupValue
{
    public IReadOnlyList<DecodedField> Fields  { get; }
    public DecodedField?               GetField(string name);
}
```

---

## Schema model types

Schema model types live in `Asterix.Codec.Schema.Models` and describe the structure of items as loaded from YAML. They are the immutable definitions the decoders and encoders are parameterised by.

| Type | Purpose |
|---|---|
| `ItemDefinition` | Abstract base for all item definitions |
| `FixedItemDefinition` | Length in bytes + ordered list of `FieldDefinition` |
| `CompoundItemDefinition` | Ordered FSPEC key list + sub-item dictionary |
| `RepetitiveItemDefinition` | Count field bits + element `ItemDefinition` |
| `FspecRepetitiveItemDefinition` | Element `ItemDefinition` (count from FSPEC) |
| `VariableItemDefinition` | List of `VariableGroupDefinition` |
| `ExplicitItemDefinition` | Marker; no structure |
| `StructuredExplicitItemDefinition` | Ordered list of `StructuredExplicitContentEntry` |

### FieldDefinition

```csharp
public sealed class FieldDefinition
{
    public string         Name        { get; }
    public FieldType      Type        { get; }   // UInt, Int, Bool, String
    public int            Bits        { get; }
    public int            BitOffset   { get; }   // offset from start of item in bits
    public ScaleFactor?   Scale       { get; }
    public StringEncoding Encoding    { get; }   // Ia5, Ascii
    public int            StringLength{ get; }   // character count for string fields
}
```

`BitOffset` is resolved at schema-load time from either an explicit `bit:` declaration or by accumulating field widths in declaration order.

### ScaleFactor

```csharp
public sealed class ScaleFactor
{
    public double Numerator   { get; }
    public double Denominator { get; }
    public double Value       => Numerator / Denominator;
}
```

YAML syntax supports both decimal (`scale: 0.25`) and rational (`scale: 1/128`, `scale: 360/65536`) forms.
