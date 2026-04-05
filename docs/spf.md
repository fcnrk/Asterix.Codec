# Special Purpose Fields (SPF)

This document describes how Special Purpose Fields (SP/RE items) are decoded and encoded using a separately-loaded YAML field set definition.

---

## Overview

ASTERIX SP (Special Purpose) and RE (Reserved Expansion) fields are explicit items: on the wire they are a length byte followed by opaque content bytes. Without a schema, the library treats them as `ExplicitDecodedItem` and preserves the content verbatim.

When a `SpfFieldSetDefinition` is registered, the library can fully decode the content into a typed `SpfDecodedItem` and re-encode it faithfully. The decoding and encoding logic is **completely generic** — no category-specific code is involved.

---

## YAML structure

SPF schemas are loaded separately from category schemas. A single YAML file can contain multiple field set definitions keyed by name.

**Reference:** `samples/spf_custom_062.yml`

```yaml
schema_version: 1

field_sets:
  SPF_CUSTOM_062:
    name: "Custom SPF for CAT062"
    description: "..."

    structure:
      - name: length
        type: scalar
        field_type: uint
        bits: 8

      - name: f1RecordCount
        type: scalar
        field_type: uint
        bits: 8

      - name: f1
        type: repetitive
        count_ref: f1RecordCount
        element:
          fields:
            - name: f2
              type: uint
              bits: 8
            - name: f3
              type: uint
              bits: 8

      - name: presenceFlags
        type: dynamic_presence
        bit_width: 1
        fields: [f4, f5, f6, f7, f8]

      - name: f4
        type: optional
        present_if: presenceFlags
        present_field: f4
        field:
          type: uint
          bits: 8

      # ... f5 through f8 follow the same pattern
```

---

## Structure entry types

SPF structure is defined as an ordered list of entries. Decoding follows this order strictly.

### scalar

```yaml
- name: length
  type: scalar
  field_type: uint   # or int
  bits: 8
```

Reads a fixed-width integer. The decoded value is stored in `DecodeContext` for use by later `count_ref` references. Typically the first entry is the total length field.

### repetitive

```yaml
- name: f1
  type: repetitive
  count_ref: f1RecordCount
  element:
    fields:
      - name: f2
        type: uint
        bits: 8
```

Reads N groups of fields, where N is the value of the scalar named by `count_ref`. The referenced scalar must appear earlier in the structure list.

### dynamic_presence

```yaml
- name: presenceFlags
  type: dynamic_presence
  bit_width: 1
  fields: [f4, f5, f6, f7, f8]
```

Reads one bit (or `bit_width` bits) per field name in the `fields` list. Each value is stored as a presence flag: 0 = absent, non-zero = present. The presence values are stored in `DecodeContext` under the group name.

### optional

```yaml
- name: f4
  type: optional
  present_if: presenceFlags   # name of the dynamic_presence entry
  present_field: f4           # field name within that presence group
  field:
    type: uint
    bits: 8
```

Decodes the field only when the referenced presence flag is non-zero. If absent, the field is not included in the decoded output (not populated with null).

---

## Decoding order

The SPF decoder enforces the following structural order:

1. **length** — total content size (boundary enforcement)
2. **count fields** (scalars used as `count_ref` targets)
3. **repetitive blocks**
4. **dynamic presence flags**
5. **conditional (optional) fields**

This order is not enforced by position in the YAML — it reflects the wire format contract for ASTERIX SPF structures. The YAML must list entries in this order; the decoder follows declaration order exactly.

---

## SpfDecoder

**File:** `Asterix.Codec/Decode/SpfDecoder.cs`

```csharp
public static SpfDecodedItem Decode(
    ref BitReader reader,
    SpfFieldSetDefinition definition,
    DecodeMode mode);
```

### Algorithm

```
1. For each structure entry in definition.Structure (in order):

   scalar:
     Read bits → store in DecodeContext and SpfDecodedItem.Fields

   repetitive:
     N = DecodeContext[count_ref]
     Decode N × element field groups
     Store List<SpfGroupValue> in Fields

   dynamic_presence:
     For each field name in entry.Fields:
       Read bit_width bits → store as presence flag in DecodeContext[groupName][fieldName]
     Store IReadOnlyDictionary<string, ulong> in Fields

   optional:
     If DecodeContext[present_if][present_field] != 0:
       Read field bits → DecodedField
       Store in Fields
     Else:
       Store null in Fields (absent)

2. Return SpfDecodedItem(fields)
```

### DecodeContext

**File:** `Asterix.Codec/Decode/DecodeContext.cs`

A simple name→value map used during SPF decode to resolve forward references:

- Scalar values stored by name (for `count_ref` lookup)
- Presence flag groups stored by group name + field name (for `present_if` lookup)

`DecodeContext` is created fresh per SPF block decode and is not exposed outside the decoder.

---

## SpfEncoder

**File:** `Asterix.Codec/Encode/SpfEncoder.cs`

```csharp
public static void Encode(
    BitWriter writer,
    SpfDecodedItem item,
    SpfFieldSetDefinition definition);
```

Mirrors the decoder structure entry by entry:

| Entry type | Encode behaviour |
|---|---|
| `scalar` | Write `RawValue` of the `DecodedField` stored under the entry name |
| `repetitive` | Recompute count from list length; write count; encode each group |
| `dynamic_presence` | Recompute presence bits from which optional fields are non-null in the item |
| `optional` | If present (non-null in item): encode the field; if absent: write nothing |

**Important:** The encoder recomputes the `length` scalar from the actual encoded byte count, not from the decoded `RawValue`. This means that if the SPF content changes size (e.g. different optional fields present), the length field is always correct.

Similarly, `count_ref` counts are recomputed from the actual list size in `SpfDecodedItem`, not from the decoded scalar. This ensures internal consistency even when a `SpfDecodedItem` is constructed programmatically.

---

## Registering an SPF field set

SPF field sets are registered via the builder before `Build()` is called:

```csharp
AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml(cat062Path)
    .AddSpfFieldSetFromYaml(spfPath)          // loads SPF_CUSTOM_062
    .Build();
```

`AddSpfFieldSetFromYaml` loads a `SpfFieldSetSchema` containing one or more field set definitions. The schema registry holds these definitions; they are not linked to a specific category at load time.

To decode an SP field using a registered definition, use `SpfDecoder` directly on the `ExplicitDecodedItem.Content` bytes:

```csharp
if (record.TryGet("SP", out var raw) && raw is ExplicitDecodedItem sp)
{
    var reader = new BitReader(sp.Content);
    SpfDecodedItem decoded = SpfDecoder.Decode(ref reader, spfDef, DecodeMode.Strict);
}
```

---

## Validation

At `Build()` time, `SchemaValidator` checks all SPF field set definitions:

- All `count_ref` values must name an earlier `scalar` entry in the same structure
- All `present_if` values must name an earlier `dynamic_presence` entry
- All field names in `present_if` references must exist in the named presence group
- All `scalar` and `optional` field bit widths must be > 0

Errors are reported as `SchemaValidationException` with the field set name and entry path.
