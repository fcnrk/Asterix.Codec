# Structured Explicit Items

This document describes the structured-explicit item feature: how a bare `type: explicit` item in a category schema can be decorated with an inner structure, enabling full typed decode and encode of RE and SP field content.

---

## Background

ASTERIX `explicit` items (SP and RE fields) carry a length byte followed by application-defined content. Without additional information, the library preserves this content as a raw `byte[]` inside an `ExplicitDecodedItem`.

The structured-explicit feature allows the inner structure of such an item to be declared in a separate YAML file and registered at build time. The library then substitutes the opaque `ExplicitItemDefinition` with a `StructuredExplicitItemDefinition` and decodes the content into a `StructuredExplicitDecodedItem` with named inner items.

---

## YAML structure

Structured-explicit schemas live in a separate file, keyed by category and item ID.

**Reference:** `samples/structured_explicit_cat253.yml`

```yaml
schema_version: 1

category: 253

items:
  I253_100:
    content:
      - id: position
        type: fixed
        length: 6
        fields:
          - name: track_id
            type: uint
            bits: 16
          - name: latitude
            type: uint
            bits: 16
            scale: 180/65536
          - name: longitude
            type: uint
            bits: 16
            scale: 360/65536

      - id: transponder
        type: variable
        groups:
          - fields:
              - name: alert
                type: bool
                bits: 1
              - name: spi
                type: bool
                bits: 1
              - name: squawk
                type: uint
                bits: 4
              - name: spare
                type: uint
                bits: 1

      - id: measurements
        type: repetitive
        count_field:
          bits: 8
        element:
          type: fixed
          length: 3
          fields:
            - name: sensor_id
              type: uint
              bits: 8
            - name: quality
              type: uint
              bits: 8
            - name: range
              type: uint
              bits: 8

      - id: nav_data
        type: compound
        fspec: [nav_data/ALT, nav_data/SPD, nav_data/HDG]
        subitems:
          nav_data/ALT:
            type: fixed
            length: 2
            fields:
              - name: altitude
                type: int
                bits: 16
                scale: 0.25
          # ...
```

Each `content` entry has an `id` and a full item definition using any of the supported item types: `fixed`, `compound`, `repetitive`, `variable`.

---

## Registration

Structured-explicit schemas are registered via the builder before `Build()`:

```csharp
AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml(cat253Path)
    .AddStructuredExplicitItemsFromYaml(structuredExplicitPath)
    .Build();
```

At `Build()` time (freeze time), the registry:

1. Looks up each item ID declared in the structured-explicit schema against the corresponding category schema
2. Verifies the target item is defined as `type: explicit` in the category schema
3. Substitutes the `ExplicitItemDefinition` with a `StructuredExplicitItemDefinition` containing the inner content list

This substitution is transparent to the caller: the item ID in the category UAP and decoded record remains the same.

---

## Schema types

### StructuredExplicitItemSetSchema

**File:** `Asterix.Codec/Schema/Models/Category/StructuredExplicitItemSetSchema.cs`

Top-level schema object loaded from the YAML file. Contains a category number and a dictionary of item ID → `StructuredExplicitItemDefinition`.

### StructuredExplicitItemDefinition

**File:** `Asterix.Codec/Schema/Models/Category/StructuredExplicitItemDefinition.cs`

```csharp
public sealed class StructuredExplicitItemDefinition : ItemDefinition
{
    public IReadOnlyList<StructuredExplicitContentEntry> Content { get; }
}
```

Replaces `ExplicitItemDefinition` for the affected item after freeze-time resolution.

### StructuredExplicitContentEntry

**File:** `Asterix.Codec/Schema/Models/Category/StructuredExplicitContentEntry.cs`

```csharp
public sealed class StructuredExplicitContentEntry
{
    public string         Id         { get; }
    public ItemDefinition Definition { get; }
}
```

One named inner item. `Definition` can be any `ItemDefinition` subtype.

---

## Decoding

### StructuredExplicitItemDecoder

**File:** `Asterix.Codec/Decode/ItemDecoders/StructuredExplicitItemDecoder.cs`

```
1. Read LEN byte → total content length (LEN includes the LEN byte itself)
2. Slice exactly (LEN - 1) bytes into a local BitReader
3. For each StructuredExplicitContentEntry in definition.Content (in declaration order):
   a. ItemDecoderDispatcher.Decode(ref localReader, entry.Definition, itemPath + "." + entry.Id, mode)
   b. Store result under entry.Id
4. Return StructuredExplicitDecodedItem(items)
```

The local reader scope ensures that inner decoding cannot read past the explicit item's boundary. In strict mode, any bytes remaining in the local reader after all entries are decoded raise a `DecodeException`. In lenient mode, they are silently ignored.

---

## Encoding

### StructuredExplicitItemEncoder

**File:** `Asterix.Codec/Encode/ItemEncoders/StructuredExplicitItemEncoder.cs`

```
1. Encode all inner items into a temporary BitWriter:
   For each entry.Id in definition.Content (in declaration order):
     ItemEncoderDispatcher.Encode(tempWriter, item.Items[entry.Id], entry.Definition, ...)
2. LEN = tempWriter.ByteLength + 1   (computed after encoding)
3. Write LEN byte to outer writer
4. Write tempWriter bytes to outer writer
```

LEN is computed after inner encoding because inner item sizes are not known in advance (compound inner items have variable FSPEC lengths, repetitive items have variable counts, etc.).

---

## Decoded model

### StructuredExplicitDecodedItem

**File:** `Asterix.Codec/Model/StructuredExplicitDecodedItem.cs`

```csharp
public sealed class StructuredExplicitDecodedItem : DecodedItem
{
    public IReadOnlyDictionary<string, DecodedItem> Items { get; }
}
```

Keys are the `id` values from the structured-explicit schema. Values are typed `DecodedItem` instances decoded according to each entry's definition.

```csharp
// Access example
if (record.TryGet("I253_100", out var raw)
    && raw is StructuredExplicitDecodedItem se)
{
    if (se.Items["position"] is FixedDecodedItem pos)
        Console.WriteLine(pos.GetField("latitude")?.ScaledValue);

    if (se.Items["nav_data"] is CompoundDecodedItem nav)
        foreach (var (key, sub) in nav.Subitems)
            Console.WriteLine(key);
}
```

---

## Round-trip correctness

Because the encoder always uses `RawValue` for all fields, and because the inner item structure is decoded and re-encoded in the same declaration order, encode(decode(x)) is byte-for-byte identical to x for any valid structured-explicit item.

The LEN byte is always recomputed from the actual encoded content length — it is not stored in `StructuredExplicitDecodedItem` and not read from any `RawValue`.

---

## Validation

At `Build()` time:

- The target item ID must exist in the category's item dictionary
- The target item must have `ExplicitItemDefinition` in the category schema; any other type is a configuration error
- The `content` list must not be empty
- All content entry IDs must be unique within the list

Inner item definitions are validated by the same `SchemaValidator` rules that apply to regular category items (compound FSPEC references, repetitive count field bits > 0, etc.).
