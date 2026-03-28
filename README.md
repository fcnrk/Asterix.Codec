# Asterix.Codec

A .NET library for encoding and decoding [Eurocontrol ASTERIX](https://www.eurocontrol.int/asterix) binary messages. Fully schema-driven: category definitions and custom SPF field sets are loaded from YAML files at startup, with no category-specific logic in the library itself.

**Targets:** `netstandard2.0` (.NET Framework 4.6.1+, Mono) and `net9.0`.

---

## Table of contents

1. [Quick start](#quick-start)
2. [Building the codec](#building-the-codec)
3. [Decoding](#decoding)
   - [Fixed items](#fixed-items)
   - [Scaled fields](#scaled-fields)
   - [Compound items](#compound-items)
   - [Repetitive items](#repetitive-items)
   - [String fields](#string-fields)
   - [The decoded model](#the-decoded-model)
4. [Encoding](#encoding)
5. [Round-trip verification](#round-trip-verification)
6. [SPF (Special Purpose Field)](#spf-special-purpose-field)
   - [Decoding SPF](#decoding-spf)
   - [Encoding SPF](#encoding-spf)
7. [Discriminated multi-message categories](#discriminated-multi-message-categories)
8. [Structured-explicit items](#structured-explicit-items)
9. [Decode modes](#decode-modes)
10. [Schema YAML reference](#schema-yaml-reference)
    - [Category schema](#category-schema)
    - [Item types](#item-types)
    - [Field types and attributes](#field-types-and-attributes)
    - [SPF field set schema](#spf-field-set-schema)
    - [Structured-explicit item set schema](#structured-explicit-item-set-schema)
11. [Exceptions](#exceptions)
12. [Sample project](#sample-project)

---

## Quick start

```csharp
using Asterix.Codec;
using Asterix.Codec.Decode;
using Asterix.Codec.Model;

// 1. Build the codec from schema files
AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml("schemas/cat062.yml")
    .WithMode(DecodeMode.Strict)
    .Build();

// 2. Decode a raw data block
AsterixPacket packet = codec.Decode(rawBytes);

// 3. Encode back to binary
byte[] encoded = codec.Encode(packet);

// 4. Verify round-trip correctness
byte[] roundTripped = codec.RoundTrip(rawBytes);
```

The [sample project](#sample-project) (`Asterix.Codec.Sample`) contains runnable end-to-end examples for every item type.

---

## Building the codec

`AsterixCodec` instances are constructed through `AsterixCodecBuilder`. The builder is the only way to create a codec; it validates schemas eagerly so errors are caught at startup, not during a decode call.

```csharp
AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml("schemas/cat062.yml")                           // one call per category
    .AddSpfFieldSetFromYaml("schemas/spf.yml")                           // optional: custom SPF definitions
    .AddStructuredExplicitItemsFromYaml("schemas/structured_explicit_cat253.yml")  // optional: structured-explicit item schemas
    .WithMode(DecodeMode.Strict)                                          // optional: default is Strict
    .Build();
```

Schemas can also be loaded from a `Stream` (useful for embedded resources):

```csharp
using Stream stream = Assembly.GetExecutingAssembly()
    .GetManifestResourceStream("MyApp.schemas.cat062.yml")!;

AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml(stream, sourceHint: "cat062.yml")
    .Build();
```

Pre-loaded schema objects can be registered directly, bypassing YAML loading entirely:

```csharp
AsterixCategorySchema schema = YamlSchemaLoader.LoadCategory("schemas/cat062.yml");

AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategory(schema)
    .Build();
```

**`Build()` rules:**
- Throws `InvalidOperationException` if no schemas were registered.
- Freezes the internal registry — the builder cannot be reused after `Build()`.
- The resulting `AsterixCodec` is immutable and thread-safe.

---

## Decoding

```csharp
AsterixPacket packet = codec.Decode(rawBytes);
// or from a span to avoid an array allocation:
AsterixPacket packet = codec.Decode(span);
```

`AsterixPacket` carries the category number and a list of `DecodedRecord`s. An ASTERIX data block may contain multiple records.

```csharp
Console.WriteLine($"CAT{packet.Category:D3}  {packet.Records.Count} record(s)");

foreach (DecodedRecord record in packet.Records)
{
    // access items by ID
}
```

### Fixed items

A fixed item decodes to a `FixedDecodedItem` containing a list of `DecodedField`s.

```csharp
if (record.TryGet("I062_010", out DecodedItem? item) && item is FixedDecodedItem fixed_)
{
    foreach (DecodedField field in fixed_.Fields)
        Console.WriteLine($"  {field.Name} = {field.RawValue}");
}
```

`DecodedField` exposes:

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Field name from the schema |
| `RawValue` | `ulong` | Raw bit pattern off the wire |
| `ScaledValue` | `double?` | `RawValue × scale`; `null` if no scale is defined |
| `StringValue` | `string?` | Decoded string; `null` for non-string fields |

See the sample, Demo 1 (`Program.cs` lines 24–40) for a runnable example.

### Scaled fields

Some fields carry a physical unit. The schema declares a `scale` factor; the decoder multiplies the raw value and stores the result in `ScaledValue`. The encoder always works from `RawValue` — `ScaledValue` is informational only.

```csharp
// I062_070: time, scale 1/128 s
var time = fixed_.Fields.Single(f => f.Name == "time");
Console.WriteLine($"raw={time.RawValue}  scaled={time.ScaledValue} s");
// → raw=9600  scaled=75.0 s
```

See Demo 2 (`Program.cs` lines 42–58).

### Compound items

A compound item contains an inner FSPEC and a set of named sub-items, each of which is itself a `DecodedItem`. Only sub-items whose bit is set in the inner FSPEC are present.

```csharp
if (record.TryGet("I062_210", out var item) && item is CompoundDecodedItem compound)
{
    foreach (var (subId, subItem) in compound.Subitems)
    {
        if (subItem is FixedDecodedItem fi)
            Console.WriteLine($"  {subId}: {fi.Fields[0].RawValue}");
    }
}
```

See Demo 3 (`Program.cs` lines 60–88).

### Repetitive items

A repetitive item is a count-prefixed list. Each element is a `DecodedItem` (typically `FixedDecodedItem`).

```csharp
if (record.TryGet("I062_290", out var item) && item is RepetitiveDecodedItem rep)
{
    Console.WriteLine($"{rep.Count} element(s)");
    for (int i = 0; i < rep.Count; i++)
    {
        if (rep.Elements[i] is FixedDecodedItem elem)
            Console.WriteLine($"  [{i}] age={elem.Fields[0].ScaledValue} s");
    }
}
```

See Demo 4 (`Program.cs` lines 90–118).

### String fields

String fields decode to `DecodedField.StringValue`. Two encodings are supported:

| Encoding | Description |
|---|---|
| `ia5` | ICAO 6-bit packed (8 characters per 6 bytes). Used for callsigns. |
| `ascii` | 8-bit per character, null/space-trimmed on decode. |

```csharp
// I062_245: IA5 callsign
var callsign = fixed_.Fields.Single(f => f.Name == "callsign");
Console.WriteLine(callsign.StringValue); // → "BAW123"
```

See Demo 5 (`Program.cs` lines 120–134).

### The decoded model

All item types share the `DecodedItem` base class:

| Type | Used for |
|---|---|
| `FixedDecodedItem` | Fixed-length items; exposes `IReadOnlyList<DecodedField> Fields` |
| `CompoundDecodedItem` | Compound items; exposes `IReadOnlyDictionary<string, DecodedItem> Subitems` |
| `RepetitiveDecodedItem` | Repetitive items; exposes `IReadOnlyList<DecodedItem> Elements` and `int Count` |
| `VariableDecodedItem` | Variable-length FX-extensible items; exposes `IReadOnlyList<VariableGroup> Groups` |
| `ExplicitDecodedItem` | Raw explicit (SP/RE) items; exposes `ReadOnlyMemory<byte> Content` |
| `StructuredExplicitDecodedItem` | Structured-explicit items (LEN-prefixed inner schema); exposes `IReadOnlyDictionary<string, DecodedItem> Items` |
| `UnknownDecodedItem` | Lenient-mode items with no matching schema definition |

---

## Encoding

Build a `DecodedRecord` from a `Dictionary<string, DecodedItem>`, wrap it in an `AsterixPacket`, and call `Encode`:

```csharp
var record = new DecodedRecord(new Dictionary<string, DecodedItem>
{
    ["I062_010"] = new FixedDecodedItem(
    [
        new DecodedField("sac", 3, null, null),
        new DecodedField("sic", 7, null, null)
    ]),
    ["I062_040"] = new FixedDecodedItem(
    [
        new DecodedField("track_number", 1337, null, null)
    ])
});

byte[] encoded = codec.Encode(new AsterixPacket(62, [record]));
```

**Key encoding rules:**

- The encoder always uses `RawValue`. `ScaledValue` and `StringValue` for numeric fields are ignored during encoding.
- For string fields (`ia5` / `ascii`), supply the value in `StringValue` and set `RawValue = 0`.
- FSPEC bytes are recomputed from the set of present items — never stored in the model.
- Spare bits (fields with explicit `bit:` offsets that leave gaps) are written as zero.
- The 3-byte data block header (CAT + LEN) is computed and prepended automatically.

See Demo 7 (`Program.cs` lines 149–310) for encoding all CAT062 item types together.

---

## Round-trip verification

`RoundTrip` decodes then immediately re-encodes. For any well-formed payload that contains no unknown items, the result must be byte-for-byte identical to the input:

```csharp
byte[] result = codec.RoundTrip(rawBytes);
bool ok = result.SequenceEqual(rawBytes); // true for well-formed input
```

This is used in tests to confirm encoding and decoding are exact inverses. See Demo 6 (`Program.cs` lines 136–147).

---

## SPF (Special Purpose Field)

SPF (Special Purpose) fields are defined in the ASTERIX UAP as `type: explicit`. The binary wire format of an explicit item is `LEN (1 byte) + content`. The library stores only the content bytes (without the length byte) in `ExplicitDecodedItem.Content`.

A separate YAML file describes the internal structure of the SPF content. This keeps the codec generic — the library has no knowledge of what any particular SPF contains.

### Decoding SPF

Load the SPF schema, extract the content bytes, then call `SpfDecoder.Decode`:

```csharp
using Asterix.Codec.Decode;
using Asterix.Codec.Schema;

// Load the SPF definition (once at startup)
SpfFieldSetSchema spfSchema = YamlSchemaLoader.LoadSpfFieldSet("schemas/spf_custom_062.yml");
SpfFieldSetDefinition spfDef = spfSchema.FieldSets["SPF_CUSTOM_062"];

// Decode the SP item
if (record.TryGet("SP", out var item) && item is ExplicitDecodedItem sp)
{
    var reader = new BitReader(sp.Content.Span);
    SpfDecodedItem decoded = SpfDecoder.Decode(ref reader, spfDef, DecodeMode.Strict);

    // Access scalar fields
    ulong length = decoded.GetScalar("length");
    ulong count  = decoded.GetScalar("f1RecordCount");

    // Access repetitive entries
    IReadOnlyList<SpfGroupValue> f1 = decoded.GetRepetitive("f1")!;
    foreach (SpfGroupValue group in f1)
    {
        ulong f2 = group.GetField("f2")!.RawValue;
        ulong f3 = group.GetField("f3")!.RawValue;
    }

    // Access optional fields (null if absent)
    DecodedField? f4 = decoded.GetOptional("f4");   // present → DecodedField; absent → null
    string? f8 = decoded.GetOptional("f8")?.StringValue;
}
```

See Demo 7 (`Program.cs` lines 291–306) for the full decode and display loop.

### Encoding SPF

Build a `SpfDecodedItem` from a `Dictionary<string, object?>`, encode it into a `BitWriter`, then wrap the bytes in `ExplicitDecodedItem`:

```csharp
using Asterix.Codec.Binary;
using Asterix.Codec.Encode;
using Asterix.Codec.Model;

var spfWriter = new BitWriter();
SpfEncoder.Encode(spfWriter, new SpfDecodedItem(new Dictionary<string, object?>
{
    // Scalar fields: supplied by the encoder automatically (length, count)
    // so you only need to provide the data fields.

    // Repetitive field: list of SpfGroupValue
    ["f1"] = new List<SpfGroupValue>
    {
        new SpfGroupValue([new DecodedField("f2", 10, null, null),
                           new DecodedField("f3", 11, null, null)]),
        new SpfGroupValue([new DecodedField("f2", 12, null, null),
                           new DecodedField("f3", 13, null, null)])
    },

    // Optional fields: DecodedField to include, null to omit
    ["f4"] = (object?)new DecodedField("f4", 66, null, null),
    ["f5"] = (object?)null,                                        // absent
    ["f6"] = (object?)new DecodedField("f6", 0x1234, null, null),
    ["f7"] = (object?)null,                                        // absent
    ["f8"] = (object?)new DecodedField("f8", 0, null, "TEST")     // string field
}), spfDef);

byte[] spfBytes = spfWriter.ToArray();

// Wrap in an ExplicitDecodedItem (content only, no length byte)
var record = new DecodedRecord(new Dictionary<string, DecodedItem>
{
    // ... other items ...
    ["SP"] = new ExplicitDecodedItem(spfBytes)
});
```

The encoder computes the `length` field and the presence flags for optional fields automatically based on which fields are non-null in the dictionary.

See Demo 7 (`Program.cs` lines 156–170) for the full SPF encoding block.

---

## Discriminated multi-message categories

Some ASTERIX categories (such as CAT253) embed a **message type** field in every record. Its value determines which UAP applies to the rest of the record — different message types carry completely different item sets. The library calls this a *discriminated* category.

### Defining a discriminated category

In the category YAML, add a `discriminator` block and a `discriminator` value on each message definition:

```yaml
discriminator:
  item: I253_010        # item ID that carries the type value
  field: message_type   # field within that item

messages:
  - id: msg001
    name: "Type 001 — Status"
    discriminator: "1"
    uap: [I253_010, I253_001]

  - id: msg100
    name: "Type 100 — Application Data"
    discriminator: "100"
    uap: [I253_010, I253_100]
```

Rules enforced by schema validation:
- The discriminator item must be `type: fixed` and listed first in every message UAP.
- Every message must have a unique, non-empty `discriminator` string.
- A single-message category may not have a `discriminator` block.

### Decoding discriminated records

The codec selects the correct UAP automatically — no changes are needed at the call site:

```csharp
AsterixPacket packet = codec.Decode(rawBytes);
DecodedRecord record = packet.Records[0];

// Discriminator item is always present
var disc = (FixedDecodedItem)record.Items["I253_010"];
ulong msgType = disc.Fields[0].RawValue;  // 1 or 100

// Other items depend on which message type was selected
if (record.Items.TryGetValue("I253_001", out var item) && item is FixedDecodedItem status)
    Console.WriteLine($"status = {status.Fields[0].RawValue}");
```

In `Lenient` mode, an unknown discriminator value falls back to the first message definition. In `Strict` mode it throws `DecodeException`.

---

## Structured-explicit items

A *structured-explicit* item uses the standard `type: explicit` wire format (1-byte LEN prefix, followed by LEN−1 content bytes) but its inner content is decoded by a user-defined sequential schema instead of being returned as raw bytes. This is used for application data containers in discriminated categories.

### Setup

The inner schema lives in a separate `structured_explicit_cat*.yml` file. Register it alongside the category schema at build time:

```csharp
AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml("schemas/cat253.yml")
    .AddStructuredExplicitItemsFromYaml("schemas/structured_explicit_cat253.yml")
    .Build();
```

The item is declared as `type: explicit` in the category schema; the registry replaces it with the structured definition at `Build()` time.

### Decoding a structured-explicit item

The decoded item is a `StructuredExplicitDecodedItem` whose `Items` dictionary maps inner-item IDs to `DecodedItem` instances of any type (fixed, variable, repetitive, compound, …):

```csharp
if (record.TryGet("I253_100", out var raw) && raw is StructuredExplicitDecodedItem seItem)
{
    var pos = (FixedDecodedItem)seItem.Items["position"];
    Console.WriteLine($"track_id = {pos.GetField("track_id")!.RawValue}");

    var nav = (CompoundDecodedItem)seItem.Items["nav_data"];
    if (nav.Subitems.TryGetValue("nav_data/ALT", out var altItem) && altItem is FixedDecodedItem alt)
        Console.WriteLine($"altitude = {alt.GetField("altitude")!.ScaledValue} m");
}
```

### Encoding a structured-explicit item

Build a `StructuredExplicitDecodedItem` with a dictionary of inner items and include it in the record like any other item. The encoder writes the LEN byte automatically:

```csharp
var record = new DecodedRecord(new Dictionary<string, DecodedItem>
{
    ["I253_010"] = new FixedDecodedItem([new DecodedField("message_type", 100, null, null)]),
    ["I253_100"] = new StructuredExplicitDecodedItem(new Dictionary<string, DecodedItem>
    {
        ["position"] = new FixedDecodedItem(
        [
            new DecodedField("track_id",  7,   null, null),
            new DecodedField("latitude",  256, null, null),
            new DecodedField("longitude", 512, null, null),
        ]),
        // ... other inner items ...
    }),
});
```

See Demo 8 (`Program.cs`) for the full encoding and decoding walkthrough.

---

## Decode modes

Set via `AsterixCodecBuilder.WithMode(DecodeMode.X)`. Default is `Strict`.

| Mode | Behaviour |
|---|---|
| `Strict` | Throws `DecodeException` on any schema violation: unknown category, unknown item, data block length mismatch. |
| `Lenient` | Unknown items are preserved as `UnknownDecodedItem`. Length overruns are clamped silently. Useful when consuming data from senders that include non-standard items. |

---

## Schema YAML reference

Schema files are the stable contract between the library and its users. The library will reject files with an unsupported `schema_version`.

### Category schema

```yaml
schema_version: 1

category: 62
name: "System Track Data"

messages:
  - id: default
    name: "CAT062 Default Message"
    discriminator: null

    uap:
      - I062_010
      - I062_040
      - I062_070
      # ...
      - SP        # explicit/SPF items go here like any other item

items:
  I062_010:
    # ... item definition
```

The `uap` list defines the order and bit positions of items in the FSPEC. Position 0 is bit 7 of the first FSPEC byte. Items not present in the FSPEC are absent from the decoded record.

For categories with multiple message types, add a `discriminator` block and set a `discriminator` value on each message — see [Discriminated multi-message categories](#discriminated-multi-message-categories).

### Item types

#### `fixed`

A fixed-length item with one or more fields packed contiguously.

```yaml
I062_040:
  type: fixed
  length: 2       # total item length in bytes
  fields:
    - name: track_number
      type: uint
      bits: 16
```

#### `compound`

Contains an inner FSPEC followed by a set of named sub-items. Only sub-items whose bit is set are present on the wire. Sub-items are themselves full item definitions (they can be `fixed`, nested `compound`, etc.).

```yaml
I062_210:
  type: compound
  fspec:
    - qx
    - qy
    - qvx
    - qvy
  subitems:
    qx:
      type: fixed
      length: 1
      fields:
        - name: value
          type: uint
          bits: 8
          scale: 0.25
    # ...
```

#### `repetitive`

A count-prefixed list. The `count_field` is read first, then the `element` is decoded that many times.

```yaml
I062_290:
  type: repetitive
  count_field:
    bits: 8
  element:
    type: fixed
    length: 2
    fields:
      - name: age
        type: uint
        bits: 16
        scale: 1/128
```

#### `variable`

A sequence of fixed-width groups, each followed by an FX bit. Reading continues while FX = 1. Each group defines the fields for that octect (excluding the FX bit).

```yaml
SomeItem:
  type: variable
  groups:
    - fields:
        - name: flag_a
          type: bool
        - name: value
          type: uint
          bits: 6
```

#### `explicit`

Used for SP (Special Purpose) and RE (Reserved Expansion) fields. Wire format is `LEN (1 byte) + content`. The library stores only the content; the length byte is recomputed on encode.

```yaml
SP:
  type: explicit
```

### Field types and attributes

| Attribute | Required | Description |
|---|---|---|
| `name` | Yes | Field identifier |
| `type` | Yes | `uint`, `int`, `bool`, `string` |
| `bits` | Conditional | Width in bits. Required for `uint`/`int`. Inferred for `bool` (1 bit) and `string` (from `length`). |
| `bit` | No | Explicit bit offset from start of item. Use when spare bits precede this field. |
| `scale` | No | LSB weight as a number (`0.25`) or fraction (`1/128`). Sets `ScaledValue = RawValue × scale`. |
| `encoding` | Conditional | Required for `string`: `ia5` or `ascii`. |
| `length` | Conditional | Required for `string`: byte length of the field. |

**Spare bits** are modelled implicitly: if a field declares `bit: 4` but the previous field ended at bit 2, bits 2–3 are spare and decoded as zero, encoded as zero.

### SPF field set schema

```yaml
schema_version: 1

spf_field_sets:

  SPF_CUSTOM_062:
    description: "Custom SPF for CAT062"
    structure:

      - name: length
        type: uint
        bits: 16

      - name: f1RecordCount
        type: uint
        bits: 8

      - name: f1
        type: repetitive
        count_ref: f1RecordCount   # name of a scalar field decoded earlier
        element:
          type: group
          fields:
            - name: f2
              type: uint
              bits: 8
            - name: f3
              type: uint
              bits: 8

      - name: presence
        type: dynamic_presence
        bit_width: 8               # bits per presence flag (one flag per listed field)
        fields: [f4, f5, f6, f7, f8]

      - name: f4
        type: optional
        present_if: presence.f4    # group.field reference into a dynamic_presence entry
        field:
          type: uint
          bits: 8

      - name: f8
        type: optional
        present_if: presence.f8
        field:
          type: string
          encoding: ascii
          length: 4
```

SPF entry types:

| Type | Description |
|---|---|
| `uint` / `int` | Scalar integer field |
| `repetitive` | Repeated group; count taken from a previously decoded scalar via `count_ref` |
| `dynamic_presence` | One presence flag per field in the `fields` list; controls which `optional` entries follow |
| `optional` | Present only when the referenced presence flag is non-zero; `present_if: <group>.<field>` |

The full example schema is at `samples/spf_custom_062.yml`.

### Structured-explicit item set schema

A structured-explicit item set is loaded from a separate YAML file and registered with `AddStructuredExplicitItemsFromYaml`. Each entry in `items` defines the sequential inner content of one `type: explicit` item in the corresponding category schema.

```yaml
schema_version: 1

category: 253
name: "CAT253 Structured-Explicit Application Items"

items:
  I253_100:
    description: "Custom inner message"
    content:

      # Any item type may appear here, decoded in order with no FSPEC.

      - id: position
        type: fixed
        length: 6
        fields:
          - name: track_id
            type: uint
            bits: 16
          - name: latitude
            type: int
            bits: 16
            scale: 180/65536
          - name: longitude
            type: int
            bits: 16
            scale: 360/65536

      - id: transponder
        type: variable
        groups:
          - fields:
              - name: alert
                type: bool
                bits: 1
              - name: squawk
                type: uint
                bits: 4
              # do NOT declare the FX bit — the codec handles it implicitly

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

      - id: nav_data
        type: compound
        fspec: [nav_data/ALT, nav_data/SPD]
        subitems:
          nav_data/ALT:
            type: fixed
            length: 2
            fields:
              - name: altitude
                type: int
                bits: 16
                scale: "0.25"
```

**Key rules:**
- `content` is an ordered list decoded sequentially; there is no FSPEC.
- Each entry has a required `id` (unique within the item) and a full item definition.
- All item types are supported: `fixed`, `variable`, `repetitive`, `compound`.
- The FX bit in `variable` groups is managed implicitly — do not declare it as a named field.
- The wire format is `LEN (1 byte, total including itself) + content bytes`. The library handles LEN on both encode and decode.

The full example schema is at `samples/structured_explicit_cat253.yml`.

---

## Exceptions

All exceptions derive from `AsterixCodecException`.

| Exception | Thrown when |
|---|---|
| `DecodeException` | Malformed data block or schema mismatch in strict mode. Carries `ByteOffset` and `FieldPath`. |
| `EncodeException` | A required field is missing or a schema mismatch is detected during encoding. |
| `SchemaLoadException` | A YAML file cannot be read or is structurally malformed. |
| `SchemaValidationException` | A loaded schema has invalid cross-references (e.g. UAP item not defined in `items`). |
| `UnsupportedSchemaVersionException` | The `schema_version` field in a YAML file is not supported by this version of the library. |

---

## Sample project

`Asterix.Codec.Sample` is a console application that exercises every feature. Run it with:

```
dotnet run --project Asterix.Codec.Sample
```

| Demo | What it shows |
|---|---|
| Demo 1 | Decoding fixed items (`I062_010`, `I062_040`) |
| Demo 2 | Scaled time field (`I062_070`, scale 1/128 s) |
| Demo 3 | Compound item decode (`I062_210` track quality) |
| Demo 4 | Repetitive item decode (`I062_290` track ages) |
| Demo 5 | IA5 string decode (`I062_245` callsign) |
| Demo 6 | Round-trip verification for multiple packet types |
| Demo 7 | Full CAT062 packet with all 13 UAP items, including an SP field carrying `SPF_CUSTOM_062` |
| Demo 8 | CAT253 discriminated messages: Type 001 (status) and Type 100 (structured-explicit container with fixed, variable, repetitive, and compound inner items) |

The schemas loaded by the sample are in `Asterix.Codec.Sample/schemas/` and are copied to the output directory on build. They are also the authoritative examples referenced throughout this document:

- `schemas/cat062.yml` — CAT062 System Track Data definition
- `schemas/spf_custom_062.yml` — Custom SPF field set with repetitive, dynamic presence, and optional fields
- `schemas/cat253.yml` — CAT253 discriminated multi-message category definition
- `schemas/structured_explicit_cat253.yml` — Inner structure of the `I253_100` structured-explicit item
