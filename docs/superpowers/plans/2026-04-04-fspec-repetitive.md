# `fspec_repetitive` Item Type Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new `fspec_repetitive` YAML item type that decodes a list of N identical elements where N is determined by the number of set bits in an FSPEC (with FX-bit extension), and wire it into the schema loader, validator, decoder, and encoder.

**Architecture:** All changes are additive — no existing code is modified except the two dispatcher switch statements and the schema validator's `ValidateItem` branch. The new type mirrors `repetitive` for element handling but uses `FspecParser.ReadPresence` (same as `compound`) to determine count. A dedicated decoded model type (`FspecRepetitiveDecodedItem`) is used instead of reusing `RepetitiveDecodedItem` so consumers can distinguish the count mechanism.

**Tech Stack:** C# 12, .NET 9 / netstandard2.0, xUnit, FluentAssertions, YamlDotNet.

---

## File map

| Action | File |
|---|---|
| Create | `Asterix.Codec/Schema/Models/Category/FspecRepetitiveItemDefinition.cs` |
| Create | `Asterix.Codec/Model/FspecRepetitiveDecodedItem.cs` |
| Modify | `Asterix.Codec/Schema/YamlSchemaLoader.cs` |
| Modify | `Asterix.Codec/Schema/SchemaValidator.cs` |
| Create | `Asterix.Codec/Decode/ItemDecoders/FspecRepetitiveItemDecoder.cs` |
| Modify | `Asterix.Codec/Decode/ItemDecoders/ItemDecoderDispatcher.cs` |
| Create | `Asterix.Codec/Encode/ItemEncoders/FspecRepetitiveItemEncoder.cs` |
| Modify | `Asterix.Codec/Encode/ItemEncoders/ItemEncoderDispatcher.cs` |
| Modify | `Asterix.Codec/Schema/SchemaValidator.cs` |
| Modify | `samples/cat062.yml` |
| Modify | `Asterix.Codec.Tests/Fixtures/SchemaFixtures.cs` |
| Modify | `Asterix.Codec.Tests/Fixtures/PayloadFixtures.cs` |
| Create | `Asterix.Codec.Tests/Unit/FspecRepetitiveDecoderTests.cs` |
| Create | `Asterix.Codec.Tests/Unit/FspecRepetitiveEncoderTests.cs` |
| Create | `Asterix.Codec.Tests/Integration/FspecRepetitiveIntegrationTests.cs` |

---

## Task 1: Schema model

**Files:**
- Create: `Asterix.Codec/Schema/Models/Category/FspecRepetitiveItemDefinition.cs`

- [ ] **Step 1: Create the definition class**

```csharp
namespace Asterix.Codec.Schema.Models;

/// <summary>
/// An ASTERIX item whose elements are repeated N times, where N is the number
/// of set data bits in an FSPEC prefix (FX-bit extended, same mechanism as
/// <see cref="CompoundItemDefinition"/>).
///
/// <para>
/// Unlike <see cref="RepetitiveItemDefinition"/>, the count is not written
/// explicitly on the wire — it is derived by counting the set bits in the FSPEC.
/// Unlike <see cref="CompoundItemDefinition"/>, all elements have the same structure.
/// </para>
/// </summary>
public sealed class FspecRepetitiveItemDefinition : ItemDefinition
{
    /// <summary>Structure decoded for every set FSPEC bit.</summary>
    public ItemDefinition Element { get; }

    public FspecRepetitiveItemDefinition(ItemDefinition element)
        => Element = element;
}
```

---

## Task 2: Decoded model

**Files:**
- Create: `Asterix.Codec/Model/FspecRepetitiveDecodedItem.cs`

- [ ] **Step 1: Create the decoded model**

```csharp
namespace Asterix.Codec.Model;

/// <summary>
/// A decoded ASTERIX fspec-repetitive item: a sequence of identically structured
/// elements whose count was determined by the number of set bits in an inner FSPEC.
/// </summary>
public sealed class FspecRepetitiveDecodedItem : DecodedItem
{
    /// <summary>Decoded elements in FSPEC bit order.</summary>
    public IReadOnlyList<DecodedItem> Elements { get; }

    public FspecRepetitiveDecodedItem(IReadOnlyList<DecodedItem> elements)
        => Elements = elements;

    public int Count => Elements.Count;
}
```

---

## Task 3: YAML schema loader

**Files:**
- Modify: `Asterix.Codec/Schema/YamlSchemaLoader.cs` — `MapItem` switch, lines 272–298

- [ ] **Step 1: Add the `fspec_repetitive` case to `MapItem`**

In the `MapItem` switch, after the `"explicit"` case and before the `_ =>` default, add:

```csharp
"fspec_repetitive" => new FspecRepetitiveItemDefinition(
    MapItem(dto.Element ?? throw new SchemaLoadException(hint,
        "FspecRepetitive item missing 'element'."), hint)),
```

The complete switch after this change:

```csharp
return dto.Type switch
{
    "fixed" => new FixedItemDefinition(
        dto.Length ?? throw new SchemaLoadException(hint, "Fixed item missing 'length'."),
        MapFields(dto.Fields ?? [], hint)),

    "compound" => new CompoundItemDefinition(
        dto.Fspec ?? throw new SchemaLoadException(hint, "Compound item missing 'fspec'."),
        (dto.Subitems ?? throw new SchemaLoadException(hint, "Compound item missing 'subitems'."))
        .ToDictionary(kv => kv.Key, kv => MapItem(kv.Value, hint), StringComparer.Ordinal)),

    "repetitive" => new RepetitiveItemDefinition(
        new CountFieldDefinition(
            (dto.CountField ?? throw new SchemaLoadException(hint, "Repetitive item missing 'count_field'."))
            .Bits),
        MapItem(dto.Element ?? throw new SchemaLoadException(hint, "Repetitive item missing 'element'."),
            hint)),

    "variable" => new VariableItemDefinition(
        (dto.Groups ?? throw new SchemaLoadException(hint, "Variable item missing 'groups'."))
        .Select(g => new VariableGroupDefinition(MapFields(g.Fields, hint)))
        .ToList()),

    "explicit" => new ExplicitItemDefinition(),

    "fspec_repetitive" => new FspecRepetitiveItemDefinition(
        MapItem(dto.Element ?? throw new SchemaLoadException(hint,
            "FspecRepetitive item missing 'element'."), hint)),

    _ => throw new SchemaLoadException(hint, $"Unknown item type '{dto.Type}'.")
};
```

---

## Task 4: Schema validator

**Files:**
- Modify: `Asterix.Codec/Schema/SchemaValidator.cs` — `ValidateItem` method (line 106–114)

- [ ] **Step 1: Add the `FspecRepetitiveItemDefinition` branch to `ValidateItem`**

Replace the existing `ValidateItem` method:

```csharp
private static void ValidateItem(
    string itemId,
    ItemDefinition definition,
    AsterixCategorySchema schema,
    string path)
{
    if (definition is CompoundItemDefinition compound)
        ValidateCompound(itemId, compound, schema, path);

    if (definition is FspecRepetitiveItemDefinition fspecRep)
        ValidateItem($"{itemId}.element", fspecRep.Element, schema, path);
}
```

---

## Task 5: Decoder — write failing tests first, then implement

**Files:**
- Create: `Asterix.Codec.Tests/Unit/FspecRepetitiveDecoderTests.cs`
- Create: `Asterix.Codec/Decode/ItemDecoders/FspecRepetitiveItemDecoder.cs`
- Modify: `Asterix.Codec/Decode/ItemDecoders/ItemDecoderDispatcher.cs`

### Step 5a: Write the failing tests

- [ ] **Step 1: Create the test file**

```csharp
using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using Asterix.Codec.Decode.ItemDecoders;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class FspecRepetitiveDecoderTests
{
    // Element definition used throughout: 4-byte fixed (SAC 8-bit, SIC 8-bit, STN 16-bit)
    private static FspecRepetitiveItemDefinition MakeDefinition() =>
        new(new FixedItemDefinition(4,
        [
            new FieldDefinition("sac",          FieldType.UInt, 8,  bitOffset: 0),
            new FieldDefinition("sic",          FieldType.UInt, 8,  bitOffset: 8),
            new FieldDefinition("track_number", FieldType.UInt, 16, bitOffset: 16),
        ]));

    private static FspecRepetitiveDecodedItem Decode(byte[] bytes)
    {
        var reader = new BitReader(bytes);
        return FspecRepetitiveItemDecoder.Decode(
            ref reader, MakeDefinition(), "I062_510", DecodeMode.Strict);
    }

    // ── Zero elements ─────────────────────────────────────────────────────────

    [Fact]
    public void Decode_ZeroElements_ReturnsEmptyList()
    {
        // FSPEC byte: 0x00 (no bits set, FX=0) → 0 elements
        var item = Decode([0x00]);
        item.Count.Should().Be(0);
        item.Elements.Should().BeEmpty();
    }

    // ── One element ───────────────────────────────────────────────────────────

    [Fact]
    public void Decode_OneElement_ReturnsSingleElement()
    {
        // FSPEC: 0x80 (bit 7 = 1 element, FX=0)
        // element: SAC=1, SIC=2, STN=0x0100
        var item = Decode([0x80, 0x01, 0x02, 0x01, 0x00]);

        item.Count.Should().Be(1);
        var el = item.Elements[0].Should().BeOfType<FixedDecodedItem>().Subject;
        el.GetField("sac")!.RawValue.Should().Be(1UL);
        el.GetField("sic")!.RawValue.Should().Be(2UL);
        el.GetField("track_number")!.RawValue.Should().Be(0x0100UL);
    }

    // ── Two elements ──────────────────────────────────────────────────────────

    [Fact]
    public void Decode_TwoElements_ReturnsCorrectFields()
    {
        // FSPEC: 0xC0 (bits 7+6 set, FX=0) → 2 elements
        var item = Decode(
        [
            0xC0,
            0x01, 0x02, 0x01, 0x00,   // SAC=1, SIC=2, STN=256
            0x03, 0x04, 0x02, 0x00,   // SAC=3, SIC=4, STN=512
        ]);

        item.Count.Should().Be(2);

        var el0 = item.Elements[0].Should().BeOfType<FixedDecodedItem>().Subject;
        el0.GetField("sac")!.RawValue.Should().Be(1UL);
        el0.GetField("sic")!.RawValue.Should().Be(2UL);
        el0.GetField("track_number")!.RawValue.Should().Be(256UL);

        var el1 = item.Elements[1].Should().BeOfType<FixedDecodedItem>().Subject;
        el1.GetField("sac")!.RawValue.Should().Be(3UL);
        el1.GetField("sic")!.RawValue.Should().Be(4UL);
        el1.GetField("track_number")!.RawValue.Should().Be(512UL);
    }

    // ── Seven elements — fills one FSPEC byte exactly ─────────────────────────

    [Fact]
    public void Decode_SevenElements_SingleFspecByte()
    {
        // FSPEC: 0xFE (bits 7..1 all set, FX=0) → 7 elements
        // 7 × 4-byte elements = 28 bytes + 1 FSPEC byte = 29 bytes
        var bytes = new List<byte> { 0xFE };
        for (int i = 0; i < 7; i++)
            bytes.AddRange([(byte)(i + 1), 0x00, 0x00, 0x00]);

        var item = Decode(bytes.ToArray());
        item.Count.Should().Be(7);
        for (int i = 0; i < 7; i++)
        {
            var el = item.Elements[i].Should().BeOfType<FixedDecodedItem>().Subject;
            el.GetField("sac")!.RawValue.Should().Be((ulong)(i + 1));
        }
    }

    // ── Eight elements — spills into second FSPEC byte ────────────────────────

    [Fact]
    public void Decode_EightElements_TwoFspecBytes()
    {
        // FSPEC byte 0: 0xFF (bits 7..1 set, FX=1) → 7 elements + more follow
        // FSPEC byte 1: 0x80 (bit 7 set, FX=0)     → 1 more element
        // Total: 8 elements
        var bytes = new List<byte> { 0xFF, 0x80 };
        for (int i = 0; i < 8; i++)
            bytes.AddRange([(byte)(i + 1), 0x00, 0x00, 0x00]);

        var item = Decode(bytes.ToArray());
        item.Count.Should().Be(8);
        for (int i = 0; i < 8; i++)
        {
            var el = item.Elements[i].Should().BeOfType<FixedDecodedItem>().Subject;
            el.GetField("sac")!.RawValue.Should().Be((ulong)(i + 1));
        }
    }

    // ── Strict mode: truncated data ───────────────────────────────────────────

    [Fact]
    public void Decode_Strict_TruncatedElement_ThrowsDecodeException()
    {
        // FSPEC says 1 element (4 bytes) but only 2 bytes of element data follow
        var act = () => Decode([0x80, 0x01, 0x02]);
        act.Should().Throw<DecodeException>();
    }
}
```

- [ ] **Step 2: Run the tests to confirm they all fail**

```
dotnet test Asterix.Codec.Tests --filter "FullyQualifiedName~FspecRepetitiveDecoderTests" -v minimal
```

Expected: build error — `FspecRepetitiveItemDecoder` does not exist yet.

### Step 5b: Implement the decoder

- [ ] **Step 3: Create `FspecRepetitiveItemDecoder.cs`**

```csharp
using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Decode.ItemDecoders;

/// <summary>
/// Decodes a <see cref="FspecRepetitiveItemDefinition"/> from a <see cref="BitReader"/>.
///
/// <para>
/// Reads an inner FSPEC (with FX-bit extension, identical to
/// <see cref="CompoundItemDecoder"/>) and counts the number of set data bits.
/// That count determines how many consecutive instances of
/// <see cref="FspecRepetitiveItemDefinition.Element"/> are decoded.
/// </para>
/// </summary>
internal static class FspecRepetitiveItemDecoder
{
    internal static FspecRepetitiveDecodedItem Decode(
        ref BitReader reader,
        FspecRepetitiveItemDefinition definition,
        string itemPath,
        DecodeMode mode)
    {
        bool[] presence;
        try
        {
            presence = FspecParser.ReadPresence(ref reader);
        }
        catch (InvalidOperationException ex)
        {
            throw new DecodeException(reader.ByteOffset, itemPath,
                "Failed to read inner FSPEC for fspec_repetitive item", ex);
        }

        // Count set data bits — each one means one element is present.
        int count = 0;
        foreach (bool b in presence)
            if (b) count++;

        var elements = new DecodedItem[count];
        for (int i = 0; i < count; i++)
        {
            string elementPath = $"{itemPath}[{i}]";
            elements[i] = ItemDecoderDispatcher.Decode(
                ref reader, definition.Element, elementPath, mode);
        }

        return new FspecRepetitiveDecodedItem(elements);
    }
}
```

- [ ] **Step 4: Register the decoder in `ItemDecoderDispatcher.cs`**

Add one arm to the `switch` expression in `ItemDecoderDispatcher.Decode`, after the `StructuredExplicitItemDefinition` arm and before `ExplicitItemDefinition`:

```csharp
return definition switch
{
    FixedItemDefinition fixedItem
        => FixedItemDecoder.Decode(ref reader, fixedItem, itemPath, mode),
    CompoundItemDefinition compound
        => CompoundItemDecoder.Decode(ref reader, compound, itemPath, mode),
    RepetitiveItemDefinition rep
        => RepetitiveItemDecoder.Decode(ref reader, rep, itemPath, mode),
    VariableItemDefinition variable
        => VariableItemDecoder.Decode(ref reader, variable, itemPath, mode),
    StructuredExplicitItemDefinition seItem
        => StructuredExplicitItemDecoder.Decode(ref reader, seItem, itemPath, mode),
    FspecRepetitiveItemDefinition fspecRep
        => FspecRepetitiveItemDecoder.Decode(ref reader, fspecRep, itemPath, mode),
    ExplicitItemDefinition
        => ExplicitItemDecoder.Decode(ref reader, itemPath),

    _ => throw new DecodeException(reader.ByteOffset, itemPath,
        $"No decoder registered for item type '{definition.GetType().Name}'")
};
```

- [ ] **Step 5: Run the decoder tests — all should pass**

```
dotnet test Asterix.Codec.Tests --filter "FullyQualifiedName~FspecRepetitiveDecoderTests" -v minimal
```

Expected: all 6 tests pass.

---

## Task 6: Encoder — write failing tests first, then implement

**Files:**
- Create: `Asterix.Codec.Tests/Unit/FspecRepetitiveEncoderTests.cs`
- Create: `Asterix.Codec/Encode/ItemEncoders/FspecRepetitiveItemEncoder.cs`
- Modify: `Asterix.Codec/Encode/ItemEncoders/ItemEncoderDispatcher.cs`

### Step 6a: Write the failing tests

- [ ] **Step 1: Create the test file**

```csharp
using Asterix.Codec.Binary;
using Asterix.Codec.Encode.ItemEncoders;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

public class FspecRepetitiveEncoderTests
{
    private static FspecRepetitiveItemDefinition MakeDefinition() =>
        new(new FixedItemDefinition(4,
        [
            new FieldDefinition("sac",          FieldType.UInt, 8,  bitOffset: 0),
            new FieldDefinition("sic",          FieldType.UInt, 8,  bitOffset: 8),
            new FieldDefinition("track_number", FieldType.UInt, 16, bitOffset: 16),
        ]));

    private static FixedDecodedItem MakeElement(byte sac, byte sic, ushort stn) =>
        new([
            new DecodedField("sac",          sac,  null, null),
            new DecodedField("sic",          sic,  null, null),
            new DecodedField("track_number", stn,  null, null),
        ]);

    private static byte[] Encode(FspecRepetitiveDecodedItem item)
    {
        var writer = new BitWriter();
        FspecRepetitiveItemEncoder.Encode(writer, item, MakeDefinition(), "I062_510");
        return writer.ToArray();
    }

    // ── Zero elements ─────────────────────────────────────────────────────────

    [Fact]
    public void Encode_ZeroElements_WritesSingleZeroByte()
    {
        var bytes = Encode(new FspecRepetitiveDecodedItem([]));
        // FSPEC: 0x00 (no bits set, FX=0)
        bytes.Should().Equal(0x00);
    }

    // ── One element ───────────────────────────────────────────────────────────

    [Fact]
    public void Encode_OneElement_CorrectFspecAndElement()
    {
        var bytes = Encode(new FspecRepetitiveDecodedItem([MakeElement(1, 2, 0x0100)]));
        // FSPEC: 0x80 (bit 7 set, FX=0), then SAC=1, SIC=2, STN=0x0100
        bytes.Should().Equal(0x80, 0x01, 0x02, 0x01, 0x00);
    }

    // ── Two elements ──────────────────────────────────────────────────────────

    [Fact]
    public void Encode_TwoElements_CorrectBytes()
    {
        var bytes = Encode(new FspecRepetitiveDecodedItem(
        [
            MakeElement(1, 2, 0x0100),
            MakeElement(3, 4, 0x0200),
        ]));
        // FSPEC: 0xC0 (bits 7+6, FX=0)
        bytes.Should().Equal(
            0xC0,
            0x01, 0x02, 0x01, 0x00,
            0x03, 0x04, 0x02, 0x00);
    }

    // ── Seven elements — exactly one FSPEC byte ───────────────────────────────

    [Fact]
    public void Encode_SevenElements_SingleFspecByte()
    {
        var elements = Enumerable.Range(1, 7)
            .Select(i => (DecodedItem)MakeElement((byte)i, 0, 0))
            .ToList();

        var bytes = Encode(new FspecRepetitiveDecodedItem(elements));

        // FSPEC: 0xFE (bits 7..1 all set, FX=0)
        bytes[0].Should().Be(0xFE);
        bytes.Length.Should().Be(1 + 7 * 4); // 1 FSPEC + 28 element bytes
    }

    // ── Eight elements — FSPEC extends to second byte ─────────────────────────

    [Fact]
    public void Encode_EightElements_TwoFspecBytes()
    {
        var elements = Enumerable.Range(1, 8)
            .Select(i => (DecodedItem)MakeElement((byte)i, 0, 0))
            .ToList();

        var bytes = Encode(new FspecRepetitiveDecodedItem(elements));

        // FSPEC byte 0: 0xFF (bits 7..1 all set, FX=1)
        // FSPEC byte 1: 0x80 (bit 7 set, FX=0)
        bytes[0].Should().Be(0xFF);
        bytes[1].Should().Be(0x80);
        bytes.Length.Should().Be(2 + 8 * 4); // 2 FSPEC + 32 element bytes
    }
}
```

- [ ] **Step 2: Run to confirm build failure**

```
dotnet test Asterix.Codec.Tests --filter "FullyQualifiedName~FspecRepetitiveEncoderTests" -v minimal
```

Expected: build error — `FspecRepetitiveItemEncoder` does not exist yet.

### Step 6b: Implement the encoder

- [ ] **Step 3: Create `FspecRepetitiveItemEncoder.cs`**

```csharp
using Asterix.Codec.Binary;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Model;
using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Encode.ItemEncoders;

/// <summary>
/// Encodes a <see cref="FspecRepetitiveDecodedItem"/> into <paramref name="writer"/>
/// according to <paramref name="definition"/>.
///
/// <para>
/// Writes an FSPEC prefix where every data bit is set (one bit per element),
/// with FX-bit extension for N &gt; 7. Then encodes each element via
/// <see cref="ItemEncoderDispatcher"/>.
/// </para>
/// </summary>
internal static class FspecRepetitiveItemEncoder
{
    private const int DataBitsPerByte = 7;

    internal static void Encode(
        BitWriter writer,
        FspecRepetitiveDecodedItem item,
        FspecRepetitiveItemDefinition definition,
        string itemPath)
    {
        int count = item.Elements.Count;
        WriteFspec(writer, count);

        for (int i = 0; i < count; i++)
            ItemEncoderDispatcher.Encode(
                writer, item.Elements[i], definition.Element, $"{itemPath}[{i}]");
    }

    /// <summary>
    /// Writes an FSPEC with exactly <paramref name="count"/> data bits set to 1.
    /// For count = 0, writes one zero byte (valid FSPEC with FX = 0).
    /// </summary>
    private static void WriteFspec(BitWriter writer, int count)
    {
        int fspecByteCount = count == 0 ? 1 : (count - 1) / DataBitsPerByte + 1;

        for (int byteIdx = 0; byteIdx < fspecByteCount; byteIdx++)
        {
            int firstElemInByte = byteIdx * DataBitsPerByte;
            int elemsInByte = Math.Min(DataBitsPerByte, count - firstElemInByte);
            bool isLast = byteIdx == fspecByteCount - 1;

            byte fspecByte = 0;
            for (int i = 0; i < elemsInByte; i++)
                fspecByte |= (byte)(1 << (7 - i));  // bit 7 = first element

            if (!isLast)
                fspecByte |= 0x01;  // FX = 1: more bytes follow

            writer.WriteBits(fspecByte, 8);
        }
    }
}
```

- [ ] **Step 4: Register the encoder in `ItemEncoderDispatcher.cs`**

Add one arm to the `switch` statement in `ItemEncoderDispatcher.Encode`, after the `StructuredExplicitDecodedItem` arm and before the `ExplicitDecodedItem` arm:

```csharp
switch (item, definition)
{
    case (FixedDecodedItem fixedItem, FixedItemDefinition fixedDef):
        FixedItemEncoder.Encode(writer, fixedItem, fixedDef, itemPath);
        break;
    case (CompoundDecodedItem compoundItem, CompoundItemDefinition compoundDef):
        CompoundItemEncoder.Encode(writer, compoundItem, compoundDef, itemPath);
        break;
    case (RepetitiveDecodedItem repetitiveItem, RepetitiveItemDefinition repetitiveDef):
        RepetitiveItemEncoder.Encode(writer, repetitiveItem, repetitiveDef, itemPath);
        break;
    case (VariableDecodedItem variableItem, VariableItemDefinition variableDef):
        VariableItemEncoder.Encode(writer, variableItem, variableDef, itemPath);
        break;
    case (StructuredExplicitDecodedItem seItem, StructuredExplicitItemDefinition structuredExplicitDef):
        StructuredExplicitItemEncoder.Encode(writer, seItem, structuredExplicitDef, itemPath);
        break;
    case (FspecRepetitiveDecodedItem fspecRepItem, FspecRepetitiveItemDefinition fspecRepDef):
        FspecRepetitiveItemEncoder.Encode(writer, fspecRepItem, fspecRepDef, itemPath);
        break;
    case (ExplicitDecodedItem explicitItem, ExplicitItemDefinition explicitDef):
        ExplicitItemEncoder.Encode(writer, explicitItem, explicitDef, itemPath);
        break;
    default:
        throw new EncodeException(itemPath,
            $"Cannot encode item of type '{item.GetType().Name}' " +
            $"with definition of type '{definition.GetType().Name}'");
}
```

- [ ] **Step 5: Run encoder tests — all should pass**

```
dotnet test Asterix.Codec.Tests --filter "FullyQualifiedName~FspecRepetitiveEncoderTests" -v minimal
```

Expected: all 5 tests pass.

---

## Task 7: Integration tests — update fixtures and test end-to-end

**Files:**
- Modify: `Asterix.Codec.Tests/Fixtures/SchemaFixtures.cs`
- Modify: `Asterix.Codec.Tests/Fixtures/PayloadFixtures.cs`
- Create: `Asterix.Codec.Tests/Integration/FspecRepetitiveIntegrationTests.cs`

The integration test uses the YAML file (cat062.yml, updated in Task 8) via `AsterixCodecBuilder`
**and** a programmatic schema from `SchemaFixtures`. Both paths must work.

### Payload byte layout for I062_510 with 2 entries

```
Packet: I062_010 + I062_510 (2 entries)
──────────────────────────────────────
Header:
  0x3E              CAT = 62
  0x00, 0x10        LEN = 16 bytes total

Record-level FSPEC (I062_010 at UAP pos 0, I062_510 at UAP pos 12):
  0x81              byte 0: bit7=I062_010, FX=1
  0x04              byte 1: bit2=I062_510 (pos 12 = byte1 bit2), FX=0

I062_010 (2 bytes):
  0x01, 0x02        SAC=1, SIC=2

I062_510 inner FSPEC (2 set bits → 2 elements):
  0xC0              bits 7+6 set, FX=0

I062_510 element 0 (4 bytes):
  0x01, 0x02, 0x01, 0x00    SAC=1, SIC=2, STN=256

I062_510 element 1 (4 bytes):
  0x03, 0x04, 0x02, 0x00    SAC=3, SIC=4, STN=512

Total: 3 + 2 + 2 + 1 + 4 + 4 = 16 bytes ✓
```

- [ ] **Step 1: Add `I062_510` factory method and update `Cat062Schema` in `SchemaFixtures.cs`**

Add the item factory after `I062_290`:

```csharp
public static FspecRepetitiveItemDefinition I062_510() => new(
    new FixedItemDefinition(4,
    [
        new FieldDefinition("sac",          FieldType.UInt, 8,  bitOffset: 0),
        new FieldDefinition("sic",          FieldType.UInt, 8,  bitOffset: 8),
        new FieldDefinition("track_number", FieldType.UInt, 16, bitOffset: 16),
    ]));
```

Update `Cat062Schema()` — extend the UAP and items dictionary to include `I062_510`:

```csharp
public static AsterixCategorySchema Cat062Schema() => new(
    category: 62,
    name: "System Track Data",
    schemaVersion: 1,
    messages:
    [
        new MessageDefinition(
            id: "default",
            name: "CAT062 Default Message",
            discriminator: null,
            uap: ["I062_010","I062_015","I062_040","I062_060","I062_070",
                  "I062_105","I062_100","I062_185","I062_210","I062_245",
                  "I062_380","I062_290","I062_510"])
    ],
    items: new Dictionary<string, ItemDefinition>
    {
        ["I062_010"] = I062_010(),
        ["I062_015"] = I062_015(),
        ["I062_040"] = I062_040(),
        ["I062_060"] = I062_060(),
        ["I062_070"] = I062_070(),
        ["I062_105"] = I062_105(),
        ["I062_100"] = I062_100(),
        ["I062_185"] = I062_185(),
        ["I062_210"] = I062_210(),
        ["I062_245"] = I062_245(),
        ["I062_380"] = I062_380(),
        ["I062_290"] = I062_290(),
        ["I062_510"] = I062_510(),
    });
```

- [ ] **Step 2: Add the payload fixture to `PayloadFixtures.cs`**

Add after `Cat062WithRepetitive`:

```csharp
// ── CAT062 with fspec-repetitive: I062_010 + I062_510 (2 entries) ─────────
//
// Record-level FSPEC:
//   byte 0: 0x81  (bit7=I062_010, FX=1)
//   byte 1: 0x04  (bit2=I062_510 at UAP pos 12, FX=0)
//
// I062_010:       0x01, 0x02        → SAC=1, SIC=2
//
// I062_510 inner FSPEC:
//   0xC0          (bits 7+6 set → 2 elements, FX=0)
//
// element[0]:     0x01, 0x02, 0x01, 0x00  → SAC=1, SIC=2, STN=256
// element[1]:     0x03, 0x04, 0x02, 0x00  → SAC=3, SIC=4, STN=512
//
// Total: 3 (header) + 2 (FSPEC) + 2 (I062_010) + 1+4+4 (I062_510) = 16
public static readonly byte[] Cat062WithFspecRepetitive =
[
    0x3E, 0x00, 0x10,           // header: CAT=62, LEN=16
    0x81, 0x04,                 // FSPEC: I062_010 + I062_510
    0x01, 0x02,                 // I062_010: SAC=1, SIC=2
    0xC0,                       // I062_510 inner FSPEC: 2 elements
    0x01, 0x02, 0x01, 0x00,    // element[0]: SAC=1, SIC=2, STN=256
    0x03, 0x04, 0x02, 0x00,    // element[1]: SAC=3, SIC=4, STN=512
];
```

- [ ] **Step 3: Write the integration test file**

```csharp
using Asterix.Codec.Decode;
using Asterix.Codec.Model;
using Asterix.Codec.Schema;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Integration;

public class FspecRepetitiveIntegrationTests
{
    private static AsterixDecoder BuildDecoder()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        return new AsterixDecoder(registry, DecodeMode.Strict);
    }

    // ── Decode ────────────────────────────────────────────────────────────────

    [Fact]
    public void Decode_I062_510_TwoEntries_CorrectElements()
    {
        var packet = BuildDecoder().Decode(PayloadFixtures.Cat062WithFspecRepetitive);

        packet.Category.Should().Be(62);
        packet.Records.Should().HaveCount(1);

        var item510 = packet.Records[0].Items["I062_510"]
            .Should().BeOfType<FspecRepetitiveDecodedItem>().Subject;

        item510.Count.Should().Be(2);

        var el0 = item510.Elements[0].Should().BeOfType<FixedDecodedItem>().Subject;
        el0.GetField("sac")!.RawValue.Should().Be(1UL);
        el0.GetField("sic")!.RawValue.Should().Be(2UL);
        el0.GetField("track_number")!.RawValue.Should().Be(256UL);

        var el1 = item510.Elements[1].Should().BeOfType<FixedDecodedItem>().Subject;
        el1.GetField("sac")!.RawValue.Should().Be(3UL);
        el1.GetField("sic")!.RawValue.Should().Be(4UL);
        el1.GetField("track_number")!.RawValue.Should().Be(512UL);
    }

    // ── Encode ────────────────────────────────────────────────────────────────

    [Fact]
    public void Encode_I062_510_TwoEntries_MatchesExpectedBytes()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        var codec = new AsterixCodec(
            new AsterixDecoder(registry, DecodeMode.Strict),
            new Asterix.Codec.Encode.AsterixEncoder(registry));

        var record = new DecodedRecord(new Dictionary<string, DecodedItem>
        {
            ["I062_010"] = new FixedDecodedItem(
            [
                new DecodedField("sac", 1, null, null),
                new DecodedField("sic", 2, null, null),
            ]),
            ["I062_510"] = new FspecRepetitiveDecodedItem(
            [
                new FixedDecodedItem(
                [
                    new DecodedField("sac",          1,   null, null),
                    new DecodedField("sic",          2,   null, null),
                    new DecodedField("track_number", 256, null, null),
                ]),
                new FixedDecodedItem(
                [
                    new DecodedField("sac",          3,   null, null),
                    new DecodedField("sic",          4,   null, null),
                    new DecodedField("track_number", 512, null, null),
                ]),
            ]),
        });

        byte[] encoded = codec.Encode(new AsterixPacket(62, [record]));
        encoded.Should().Equal(PayloadFixtures.Cat062WithFspecRepetitive);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_I062_510_TwoEntries_IsByteForByteIdentical()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();
        var codec = new AsterixCodec(
            new AsterixDecoder(registry, DecodeMode.Strict),
            new Asterix.Codec.Encode.AsterixEncoder(registry));

        byte[] result = codec.RoundTrip(PayloadFixtures.Cat062WithFspecRepetitive);
        result.Should().Equal(PayloadFixtures.Cat062WithFspecRepetitive);
    }
}
```

- [ ] **Step 4: Run the integration tests — should fail (fixture schema missing I062_510 UAP update)**

```
dotnet test Asterix.Codec.Tests --filter "FullyQualifiedName~FspecRepetitiveIntegrationTests" -v minimal
```

Expected: tests fail because `SchemaFixtures.Cat062Schema` does not yet include `I062_510`.

- [ ] **Step 5: Apply the SchemaFixtures and PayloadFixtures changes from Steps 1–2, then run again**

```
dotnet test Asterix.Codec.Tests --filter "FullyQualifiedName~FspecRepetitiveIntegrationTests" -v minimal
```

Expected: all 3 tests pass.

---

## Task 8: Update cat062.yml and run the full test suite

**Files:**
- Modify: `samples/cat062.yml`

- [ ] **Step 1: Replace the `I062_510` definition in `samples/cat062.yml`**

The existing definition uses `type: compound` with named `stn1/stn2/stn3` sub-items.
Replace it with the `fspec_repetitive` definition:

```yaml
  # ----------------------------------------------------------
  # I062/510 - Composed Track Number (FSPEC-Repetitive)
  #
  # Lists the track numbers assigned by each contributing SDPS
  # in a multi-SDPS fusion scenario.  The number of entries is
  # driven by the inner FSPEC (one set bit = one entry).
  # ----------------------------------------------------------
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

- [ ] **Step 2: Run the full test suite**

```
dotnet test Asterix.Codec.Tests -v minimal
```

Expected: all existing tests plus the new decoder, encoder, and integration tests pass. No regressions.

---

## Self-review checklist

- **Spec coverage:**
  - `FspecRepetitiveItemDefinition` → Task 1 ✓
  - YAML loader case → Task 3 ✓
  - Schema validator → Task 4 ✓
  - `FspecRepetitiveDecodedItem` → Task 2 ✓
  - Decoder + dispatcher → Task 5 ✓
  - Encoder + dispatcher → Task 6 ✓
  - N=0 edge case → Task 5 (decoder test) + Task 6 (encoder test) ✓
  - N=7 boundary (single byte full) → both test suites ✓
  - N=8 boundary (two FSPEC bytes) → both test suites ✓
  - Strict truncation error → Task 5 decoder test ✓
  - Round-trip → Task 7 integration test ✓
  - `samples/cat062.yml` update → Task 8 ✓

- **Placeholder scan:** No TBDs. All byte values computed and verified above.

- **Type consistency:**
  - `FspecRepetitiveItemDefinition` used consistently in loader, validator, decoder, encoder.
  - `FspecRepetitiveDecodedItem` used in decoder return type and encoder parameter.
  - `FspecRepetitiveItemDecoder.Decode` / `FspecRepetitiveItemEncoder.Encode` — signatures match dispatcher call sites.
