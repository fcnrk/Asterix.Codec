# Bit Operations

This document covers the low-level binary I/O layer: `BitReader`, `BitWriter`, `StringEncoders`, `FspecParser`, and `FspecBuilder`. These types sit beneath the decode and encode engines and are never exposed publicly.

---

## Conventions

All bit I/O is **MSB-first** (big-endian within each byte), which matches the ASTERIX wire format. Bit 7 is the most-significant bit of each byte and is read or written first.

---

## BitReader

**File:** `Asterix.Codec/Binary/BitReader.cs`

`BitReader` is a `ref struct` that reads bits sequentially from a `ReadOnlySpan<byte>`. It is passed by `ref` through the entire decode pipeline so that position advances are visible to the caller.

### Key properties

| Property | Description |
|---|---|
| `ByteOffset` | Current absolute byte position in the span |
| `BitOffsetInByte` | Current bit position within the current byte (0–7) |
| `IsAligned` | True when `BitOffsetInByte == 0` (on a byte boundary) |
| `RemainingBits` | Total bits left to read |

### Reading

```csharp
ulong value = reader.ReadBits(n);   // reads n bits (1–64), MSB first
byte  b      = reader.ReadByte();   // reads exactly 8 bits
```

`ReadBits` shifts bits out of the buffer MSB-first. Reads that cross byte boundaries are handled automatically.

### Slicing

```csharp
BitReader slice = reader.Slice(byteCount);
```

Returns a new `BitReader` covering exactly `byteCount` bytes, advancing the original reader past those bytes. Used by `FixedItemDecoder` to scope reads to the declared item length.

---

## BitWriter

**File:** `Asterix.Codec/Binary/BitWriter.cs`

`BitWriter` writes bits MSB-first into a dynamically-growing internal buffer. Unlike `BitReader`, it is a class (not a ref struct) because it outlives individual method calls during encoding.

### Writing

```csharp
writer.WriteBits(value, n);   // writes n bits (1–64) from value, MSB first
writer.WriteByte(b);          // writes exactly 8 bits
```

### Retrieving output

```csharp
byte[] result   = writer.ToArray();
int    bitCount = writer.BitLength;
bool   aligned  = writer.IsAligned;
```

`ToArray()` returns the accumulated bytes. If the writer is not byte-aligned (e.g. after writing a non-multiple of 8 bits), the trailing bits are zero-padded.

### Bit position

```csharp
int position = writer.BitPosition;  // total bits written so far
```

Used by `FixedItemEncoder` to verify byte alignment before and after encoding each fixed item.

---

## StringEncoders

**File:** `Asterix.Codec/Binary/StringEncoders.cs`

Handles the two string encodings defined in ASTERIX schemas.

### IA5 (6-bit packed)

ICAO callsigns are stored as 6-bit characters packed into a byte sequence. Each character maps to a 6-bit code (space = 0x20, A–Z = 0x01–0x1A, 0–9 = 0x30–0x39).

```csharp
string s = StringEncoders.DecodeIa5(bits, charCount);
ulong  v = StringEncoders.EncodeIa5(s, charCount);    // right-padded with spaces
```

A 6-character callsign occupies 36 bits (4.5 bytes), which is why IA5 fields always appear in fixed items with explicit `length:` and `bit:` declarations.

### ASCII

Standard 8-bit ASCII encoding with no packing.

```csharp
string s = StringEncoders.DecodeAscii(bits, charCount);
ulong  v = StringEncoders.EncodeAscii(s, charCount);
```

---

## FspecParser

**File:** `Asterix.Codec/Decode/FspecParser.cs`

Reads and interprets ASTERIX FSPEC (Field Specification) bytes.

### Wire format

Each FSPEC byte carries 7 data bits and 1 FX (extension) bit:

```
bit 7  bit 6  bit 5  bit 4  bit 3  bit 2  bit 1  bit 0
┌──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┐
│  P1  │  P2  │  P3  │  P4  │  P5  │  P6  │  P7  │  FX  │
└──────┴──────┴──────┴──────┴──────┴──────┴──────┴──────┘
```

FX = 1 means another FSPEC byte follows. FX = 0 means this is the last byte.

### Reading presence

```csharp
bool[] presence = FspecParser.ReadPresence(ref reader);
```

Reads FSPEC bytes until FX = 0 and returns a flat `bool[]` of presence flags indexed by UAP position (0-based). FX bits are excluded from the array. The reader is left positioned immediately after the last FSPEC byte.

Maximum FSPEC length is capped at 16 bytes (112 data bits), which exceeds any defined ASTERIX category UAP.

### Mapping to item IDs

```csharp
IReadOnlyList<string> presentIds = FspecParser.GetPresentItemIds(presence, uap);
```

Maps the presence array to ordered item ID strings using the UAP list. Presence bits beyond the UAP length are silently ignored.

---

## FspecBuilder

**File:** `Asterix.Codec/Encode/FspecBuilder.cs`

Constructs FSPEC bytes from a set of present item IDs. This is the encode-side counterpart to `FspecParser`.

FSPEC bytes are always **recomputed** during encoding — they are never stored in the decoded model. This is what guarantees round-trip correctness: the encoder always produces a valid FSPEC regardless of how the `DecodedRecord` was constructed.

### Writing to a BitWriter

```csharp
FspecBuilder.WriteFspec(uap, presentItemIds, writer);
```

Determines the minimum number of FSPEC bytes required (based on the highest-indexed present UAP item), builds each byte with bits 7–1 set for present items, and sets FX = 1 on all bytes except the last.

If no items are present, nothing is written.

### Building as a byte array

```csharp
byte[] fspec = FspecBuilder.BuildFspec(uap, presentItemIds);
```

Convenience overload for testing and serialization. Returns an empty array if no items are present.
