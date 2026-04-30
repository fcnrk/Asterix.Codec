using Asterix.Codec;
using Asterix.Codec.Binary;
using Asterix.Codec.Decode;
using Asterix.Codec.Encode;
using Asterix.Codec.Model;
using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;

string schemasDir = Path.Combine(AppContext.BaseDirectory, "schemas");
string cat062Path = Path.Combine(schemasDir, "cat062.yml");
string spfPath = Path.Combine(schemasDir, "spf_custom_062.yml");

AsterixCodec codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml(cat062Path)
    .AddSpfFieldSetFromYaml(spfPath)
    .WithMode(DecodeMode.Strict)
    .Build();

Console.WriteLine("=== Asterix.Codec Sample ===");
Console.WriteLine($"Loaded schemas from: {schemasDir}");
Console.WriteLine();

// Demo 1: Decode simple fixed items
// UAP: I062_010=FRN1 (octet1 bit7), I062_040=FRN12 (octet2 bit3)
// FSPEC octet1: 0x81 = I062_010 present + FX; octet2: 0x08 = I062_040 present
Console.WriteLine("--- Demo 1: Fixed items (I062_010 + I062_040) ---");

byte[] simplePacket =
[
    0x3E, 0x00, 0x09, // CAT=62, LEN=9
    0x81, 0x08,       // FSPEC: I062_010(FRN1) + FX + I062_040(FRN12)
    0x01, 0x02,       // I062_010: SAC=1, SIC=2
    0x12, 0x34,       // I062_040: track_number=4660
];

AsterixPacket decoded = codec.Decode(simplePacket);
Console.WriteLine($"Category: {decoded.Category}  Records: {decoded.Records.Count}");

DecodedRecord rec = decoded.Records[0];
PrintFixed(rec, "I062_010");
PrintFixed(rec, "I062_040");
Console.WriteLine();

// Demo 2: Decode with time (I062_070, scaled field)
// UAP: I062_010=FRN1 (oct1 bit7), I062_070=FRN4 (oct1 bit4), I062_040=FRN12 (oct2 bit3)
// Data order follows FRN order: I062_010, I062_070, I062_040
Console.WriteLine("--- Demo 2: Scaled time field (I062_070) ---");

byte[] timePacket =
[
    0x3E, 0x00, 0x0C, // CAT=62, LEN=12
    0x91, 0x08,       // FSPEC: I062_010(FRN1)+I062_070(FRN4)+FX + I062_040(FRN12)
    0x01, 0x02,       // I062_010: SAC=1, SIC=2
    0x00, 0x25, 0x80, // I062_070: raw=9600 → 9600/128 = 75.0 s
    0x12, 0x34,       // I062_040: track_number=4660
];

rec = codec.Decode(timePacket).Records[0];
PrintFixed(rec, "I062_010");
PrintFixed(rec, "I062_070");
PrintFixed(rec, "I062_040");
Console.WriteLine();

// Demo 3: Decode fixed acceleration item (I062_210 Calculated Acceleration)
// UAP: I062_010=FRN1 (oct1 bit7), I062_210=FRN8 (oct2 bit7)
// FSPEC: 0x81 (I062_010+FX) + 0x80 (I062_210); LEN grows by 1
// I062_210 is 2B fixed: ax=int8 scale 0.25, ay=int8 scale 0.25
Console.WriteLine("--- Demo 3: Fixed acceleration item (I062_210) ---");

byte[] compoundPacket =
[
    0x3E, 0x00, 0x09, // CAT=62, LEN=9
    0x81, 0x80,       // FSPEC: I062_010(FRN1)+FX + I062_210(FRN8)
    0x01, 0x02,       // I062_010: SAC=1, SIC=2
    0x04, 0x08,       // I062_210: ax=4→1.0 m/s², ay=8→2.0 m/s²
];

rec = codec.Decode(compoundPacket).Records[0];
PrintFixed(rec, "I062_010");
PrintFixed(rec, "I062_210");
Console.WriteLine();

// Demo 4: Decode compound item (I062_290 System Track Update Ages)
// UAP: I062_010=FRN1 (oct1 bit7), I062_290=FRN14 (oct2 bit1)
// FSPEC: 0x81 (I062_010+FX) + 0x02 (I062_290)
// I062_290 is compound: FSPEC byte then subitems (trk + psr)
Console.WriteLine("--- Demo 4: Compound item (I062_290 system track update ages) ---");

byte[] repetitivePacket =
[
    0x3E, 0x00, 0x0A, // CAT=62, LEN=10
    0x81, 0x02,       // FSPEC: I062_010(FRN1)+FX + I062_290(FRN14)
    0x01, 0x02,       // I062_010: SAC=1, SIC=2
    0xC0,             // I062_290 inner FSPEC: trk(bit7)+psr(bit6) present, FX=0
    0x28,             // I062_290/trk: raw=40 → 40×0.25=10.0 s
    0x14,             // I062_290/psr: raw=20 → 20×0.25=5.0 s
];

rec = codec.Decode(repetitivePacket).Records[0];
PrintFixed(rec, "I062_010");
if (rec.TryGet("I062_290", out var repItem) && repItem is CompoundDecodedItem compI290)
{
    Console.WriteLine($"  I062_290 (compound, {compI290.Subitems.Count} subitems):");
    foreach (var (subId, subItem) in compI290.Subitems)
    {
        if (subItem is FixedDecodedItem fi)
        {
            Console.Write($"    {subId}:");
            foreach (var f in fi.Fields)
                Console.Write($"  {f}");
            Console.WriteLine();
        }
    }
}

Console.WriteLine();

// Demo 5: Decode IA5 callsign (I062_245 Target Identification)
// UAP: I062_010=FRN1 (oct1 bit7), I062_245=FRN10 (oct2 bit5)
// FSPEC: 0x81 (I062_010+FX) + 0x20 (I062_245)
// I062_245 = 1B header (sti 2b + spare 6b) + 6B callsign (8×6-bit IA5 = "BAW123  ")
Console.WriteLine("--- Demo 5: IA5 string field (I062_245 callsign) ---");

byte[] callsignPacket =
[
    0x3E, 0x00, 0x0E,             // CAT=62, LEN=14
    0x81, 0x20,                   // FSPEC: I062_010(FRN1)+FX + I062_245(FRN10)
    0x01, 0x02,                   // I062_010: SAC=1, SIC=2
    0x00,                         // I062_245 header: sti=0, spare=0
    0x08, 0x15, 0xF1, 0xCB, 0x38, 0x20, // I062_245 callsign: "BAW123" (IA5 6-bit packed)
];

rec = codec.Decode(callsignPacket).Records[0];
PrintFixed(rec, "I062_010");
PrintFixed(rec, "I062_245");
Console.WriteLine();

// Demo 6: Round-trip verification 
Console.WriteLine("--- Demo 6: Round-trip encode → decode ---");

bool rtOk = codec.RoundTrip(compoundPacket).SequenceEqual(compoundPacket);
Console.WriteLine($"  Compound packet round-trip:   {(rtOk ? "PASS" : "FAIL")}");

rtOk = codec.RoundTrip(repetitivePacket).SequenceEqual(repetitivePacket);
Console.WriteLine($"  Repetitive packet round-trip: {(rtOk ? "PASS" : "FAIL")}");

rtOk = codec.RoundTrip(callsignPacket).SequenceEqual(callsignPacket);
Console.WriteLine($"  Callsign packet round-trip:   {(rtOk ? "PASS" : "FAIL")}");
Console.WriteLine();

// Demo 7: Full CAT062 UAP with SP field carrying SPF_CUSTOM_062
Console.WriteLine("--- Demo 7: Full CAT062 UAP + SP (SPF_CUSTOM_062) ---");

// Load the SPF definition for direct SpfEncoder / SpfDecoder use.
var spfSchema = YamlSchemaLoader.LoadSpfFieldSet(spfPath);
var spfDef = spfSchema.FieldSets["SPF_CUSTOM_062"];

// Encode the SPF payload (f1×2, f4=66, f6=4660, f8="TEST"; f5/f7 absent).
var spfWriter = new BitWriter();
SpfEncoder.Encode(spfWriter, new SpfDecodedItem(new Dictionary<string, object?>
{
    ["f1"] = new List<SpfGroupValue>
    {
        new SpfGroupValue([new DecodedField("f2", 10, null, null), new DecodedField("f3", 11, null, null)]),
        new SpfGroupValue([new DecodedField("f2", 12, null, null), new DecodedField("f3", 13, null, null)])
    },
    ["f4"] = (object?)new DecodedField("f4", 66, null, null),
    ["f5"] = (object?)null,
    ["f6"] = (object?)new DecodedField("f6", 0x1234, null, null),
    ["f7"] = (object?)null,
    ["f8"] = (object?)new DecodedField("f8", 0, null, "TEST")
}), spfDef);
byte[] spfBytes = spfWriter.ToArray();

// Build a CAT062 record covering all 13 UAP items.
var fullRecord = new DecodedRecord(new Dictionary<string, DecodedItem>
{
    // I062/010 - Data Source Identifier
    ["I062_010"] = new FixedDecodedItem(
    [
        new DecodedField("sac", 3, null, null),
        new DecodedField("sic", 7, null, null)
    ]),

    // I062/015 - Service Identification
    ["I062_015"] = new FixedDecodedItem(
    [
        new DecodedField("service_id", 1, null, null)
    ]),

    // I062/040 - Track Number
    ["I062_040"] = new FixedDecodedItem(
    [
        new DecodedField("track_number", 1337, null, null)
    ]),

    // I062/060 - Mode 3/A Code (spare bit at offset 3 is zero-padded by encoder)
    ["I062_060"] = new FixedDecodedItem(
    [
        new DecodedField("v", 0, null, null),
        new DecodedField("g", 0, null, null),
        new DecodedField("ch", 0, null, null),
        new DecodedField("mode3a", 0x123, null, null)
    ]),

    // I062/070 - Time of Track Information  (scale 1/128 s)
    // raw 9600 → 9600 / 128 = 75.0 s
    ["I062_070"] = new FixedDecodedItem(
    [
        new DecodedField("time", 9600, 75.0, null)
    ]),

    // I062/105 - Calculated Position WGS-84  (signed int32, scale 180/2^25)
    // lat raw 0x02625A00 → 40000000 * 180/33554432 ≈  214.577°  (demo values)
    // lon raw 0x009C4000 →  10240000 * 180/33554432 ≈  54.932°
    ["I062_105"] = new FixedDecodedItem(
    [
        new DecodedField("latitude",  0x02625A00, null, null),
        new DecodedField("longitude", 0x009C4000, null, null)
    ]),

    // I062/100 - Calculated Track Position (Cartesian, signed int24, scale 0.5 m)
    // x raw 1000 → 500.0 m   y raw 800 → 400.0 m
    ["I062_100"] = new FixedDecodedItem(
    [
        new DecodedField("x", 1000, 500.0, null),
        new DecodedField("y",  800, 400.0, null)
    ]),

    // I062/185 - Calculated Track Velocity (Cartesian, signed int16, scale 0.25 m/s)
    // vx raw 400 → 100.0 m/s   vy raw 200 → 50.0 m/s
    ["I062_185"] = new FixedDecodedItem(
    [
        new DecodedField("vx", 400, 100.0, null),
        new DecodedField("vy", 200,  50.0, null)
    ]),

    // I062/210 - Calculated Acceleration (Cartesian, signed int8, scale 0.25 m/s²)
    // ax raw 4 → 1.0 m/s²   ay raw 0xFC (signed -4) → -1.0 m/s²
    ["I062_210"] = new FixedDecodedItem(
    [
        new DecodedField("ax", 4,    1.0, null),
        new DecodedField("ay", 0xFC, null, null)  // -4 as unsigned byte → -1.0 m/s² after scale
    ]),

    // I062/245 - Target Identification (2b STI + 6b spare + 8×6b IA5 callsign)
    ["I062_245"] = new FixedDecodedItem(
    [
        new DecodedField("sti",      0, null, null),
        new DecodedField("spare",    0, null, null),
        new DecodedField("callsign", 0, null, "BAW123  ")
    ]),

    // I062/380 - Aircraft Derived Data (compound: adr + id)
    ["I062_380"] = new CompoundDecodedItem(new Dictionary<string, DecodedItem>
    {
        ["adr"] = new FixedDecodedItem([new DecodedField("address", 0xABCDEF, null, null)]),
        ["id"]  = new FixedDecodedItem([new DecodedField("callsign", 0, null, "TEST12")])
    }),

    // I062/290 - System Track Update Ages (compound; scale 0.25 s)
    // trk age raw 40 → 10.0 s   psr age raw 20 → 5.0 s
    ["I062_290"] = new CompoundDecodedItem(new Dictionary<string, DecodedItem>
    {
        ["trk"] = new FixedDecodedItem([new DecodedField("value", 40, 10.0, null)]),
        ["psr"] = new FixedDecodedItem([new DecodedField("value", 20,  5.0, null)])
    }),

    // I062/510 - Composed Track Number (fspec_repetitive: 3 contributing SDPS)
    // Element: SAC(8b) + SIC(8b) + track_number(16b); inner FSPEC drives count
    ["I062_510"] = new FspecRepetitiveDecodedItem(
    [
        new FixedDecodedItem([new DecodedField("sac", 1, null, null), new DecodedField("sic", 2, null, null), new DecodedField("track_number", 100, null, null)]),
        new FixedDecodedItem([new DecodedField("sac", 3, null, null), new DecodedField("sic", 4, null, null), new DecodedField("track_number", 200, null, null)]),
        new FixedDecodedItem([new DecodedField("sac", 5, null, null), new DecodedField("sic", 6, null, null), new DecodedField("track_number", 300, null, null)])
    ]),

    // SP - Special Purpose Field (explicit wrapper around SPF_CUSTOM_062 block)
    ["SP"] = new ExplicitDecodedItem(spfBytes)
});

// Encode.
byte[] fullEncoded = codec.Encode(new AsterixPacket(62, [fullRecord]));
Console.WriteLine($"  Encoded: {fullEncoded.Length} bytes  CAT=0x{fullEncoded[0]:X2}  " +
                  $"LEN=0x{fullEncoded[1]:X2}{fullEncoded[2]:X2}");

// Decode back and display all items.
DecodedRecord dr = codec.Decode(fullEncoded).Records[0];

PrintFixed(dr, "I062_010");
PrintFixed(dr, "I062_015");
PrintFixed(dr, "I062_040");
PrintFixed(dr, "I062_060");
PrintFixed(dr, "I062_070");
PrintFixed(dr, "I062_105");
PrintFixed(dr, "I062_100");
PrintFixed(dr, "I062_185");
PrintFixed(dr, "I062_210");
PrintFixed(dr, "I062_245");
PrintCompound(dr, "I062_380");
PrintCompound(dr, "I062_290");
PrintFspecRepetitive(dr, "I062_510");

if (dr.TryGet("SP", out var spRaw) && spRaw is ExplicitDecodedItem spExplicit)
{
    Console.WriteLine($"  SP: {spExplicit.Content.Length} content bytes");
    var spReader = new BitReader(spExplicit.Content);
    SpfDecodedItem decodedSpf = SpfDecoder.Decode(ref spReader, spfDef, DecodeMode.Strict);
    var f1 = decodedSpf.GetRepetitive("f1")!;
    Console.WriteLine($"    length={decodedSpf.GetScalar("length")}  f1Count={decodedSpf.GetScalar("f1RecordCount")}");
    for (int idx = 0; idx < f1.Count; idx++)
        Console.WriteLine(
            $"    f1[{idx}]: f2={f1[idx].GetField("f2")!.RawValue}  f3={f1[idx].GetField("f3")!.RawValue}");
    Console.WriteLine(
        $"    f4={decodedSpf.GetOptional("f4")?.RawValue}  f5={(decodedSpf.GetOptional("f5") is null ? "(absent)" : $"{decodedSpf.GetOptional("f5")!.RawValue}")}");
    Console.WriteLine(
        $"    f6={decodedSpf.GetOptional("f6")?.RawValue}  f7={(decodedSpf.GetOptional("f7") is null ? "(absent)" : $"{decodedSpf.GetOptional("f7")!.RawValue}")}");
    Console.WriteLine($"    f8=\"{decodedSpf.GetOptional("f8")?.StringValue}\"");
}

bool fullRt = codec.RoundTrip(fullEncoded).SequenceEqual(fullEncoded);
Console.WriteLine($"  Full packet round-trip: {(fullRt ? "PASS" : "FAIL")}");
Console.WriteLine();

// Demo 8: CAT253 — discriminated multi-message category with structured-explicit application data
Console.WriteLine("--- Demo 8: CAT253 — discriminated messages + structured-explicit item (I253_100) ---");

string cat253Path      = Path.Combine(schemasDir, "cat253.yml");
string structuredExplicitPath = Path.Combine(schemasDir, "structured_explicit_cat253.yml");

AsterixCodec cat253Codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml(cat253Path)
    .AddStructuredExplicitItemsFromYaml(structuredExplicitPath)
    .WithMode(DecodeMode.Strict)
    .Build();

// 8a: Type 001 — Status message

var type001Record = new DecodedRecord(new Dictionary<string, DecodedItem>
{
    ["I253_010"] = new FixedDecodedItem(
    [
        new DecodedField("message_type", 1, null, null),   // selects msg001 UAP
    ]),
    ["I253_001"] = new FixedDecodedItem(
    [
        new DecodedField("status", 42, null, null),
    ]),
});

byte[] type001Encoded = cat253Codec.Encode(new AsterixPacket(253, [type001Record]));
DecodedRecord type001Decoded = cat253Codec.Decode(type001Encoded).Records[0];

Console.WriteLine("  [Type 001 — Status]");
PrintFixed(type001Decoded, "I253_010");
PrintFixed(type001Decoded, "I253_001");

bool rt001 = cat253Codec.RoundTrip(type001Encoded).SequenceEqual(type001Encoded);
Console.WriteLine($"  Round-trip: {(rt001 ? "PASS" : "FAIL")}");
Console.WriteLine();

// 8b: Type 100 — structured-explicit application data
//
// I253_100 is a structured-explicit container; its inner structure comes from
// structured_explicit_cat253.yml and contains four sequential items:
//   position      (fixed)       track_id + lat + lon
//   transponder   (variable)    alert + spi + squawk + spare  [FX implicit]
//   measurements  (repetitive)  count + N × (sensor_id + quality + range)
//   nav_data      (compound)    ALT and/or SPD and/or HDG
//
// Raw-value notes:
//   latitude  scale = 180/65536  → raw 256  ≈  0.703°
//   longitude scale = 360/65536  → raw 512  ≈  2.813°
//   altitude  scale = 0.25       → raw 4000 = 1000.0 m
//   speed     scale = 0.01       → raw 25000 = 250.0 kt

var type100Record = new DecodedRecord(new Dictionary<string, DecodedItem>
{
    ["I253_010"] = new FixedDecodedItem(
    [
        new DecodedField("message_type", 100, null, null),   // selects msg100 UAP
    ]),

    ["I253_100"] = new StructuredExplicitDecodedItem(new Dictionary<string, DecodedItem>
    {
        // 1. position — fixed, 6 bytes
        ["position"] = new FixedDecodedItem(
        [
            new DecodedField("track_id",  7,   null, null),
            new DecodedField("latitude",  256, null, null),
            new DecodedField("longitude", 512, null, null),
        ]),

        // 2. transponder — variable (FX-extensible), 2 groups
        //    Group 0: primary flags (FX=1 because group 1 follows)
        //    Group 1: SSR code extension
        ["transponder"] = new VariableDecodedItem(
        [
            new List<DecodedField>            // group 0
            {
                new("alert",  0, null, null),
                new("spi",    1, null, null),
                new("squawk", 3, null, null),
                new("spare",  0, null, null),
            },
            new List<DecodedField>            // group 1 (extension)
            {
                new("ssr_code", 42, null, null),
            },
        ]),

        // 3. measurements — repetitive, count byte + 3-byte elements
        ["measurements"] = new RepetitiveDecodedItem(
        [
            new FixedDecodedItem(
            [
                new DecodedField("sensor_id", 1,   null, null),
                new DecodedField("quality",   100, null, null),
                new DecodedField("range",     50,  null, null),
            ]),
            new FixedDecodedItem(
            [
                new DecodedField("sensor_id", 2,  null, null),
                new DecodedField("quality",   80, null, null),
                new DecodedField("range",     30, null, null),
            ]),
        ]),

        // 4. nav_data — compound, ALT + SPD present (HDG absent)
        ["nav_data"] = new CompoundDecodedItem(new Dictionary<string, DecodedItem>
        {
            ["nav_data/ALT"] = new FixedDecodedItem(
            [
                new DecodedField("altitude", 4000,  1000.0, null),   // raw 4000 × 0.25 = 1000.0 m
            ]),
            ["nav_data/SPD"] = new FixedDecodedItem(
            [
                new DecodedField("speed", 25000, 250.0, null),        // raw 25000 × 0.01 = 250.0 kt
            ]),
        }),
    }),
});

byte[] type100Encoded = cat253Codec.Encode(new AsterixPacket(253, [type100Record]));
Console.WriteLine($"  [Type 100 — Structured-Explicit Application Data]  encoded {type100Encoded.Length} bytes");

DecodedRecord type100Decoded = cat253Codec.Decode(type100Encoded).Records[0];
PrintFixed(type100Decoded, "I253_010");

if (type100Decoded.TryGet("I253_100", out var structuredExplicitRaw)
    && structuredExplicitRaw is StructuredExplicitDecodedItem seItem)
{
    Console.WriteLine("  I253_100 (structured-explicit):");

    // position
    if (seItem.Items["position"] is FixedDecodedItem pos)
    {
        Console.Write("    position:");
        foreach (var f in pos.Fields) Console.Write($"  {f}");
        Console.WriteLine();
    }

    // transponder
    if (seItem.Items["transponder"] is VariableDecodedItem xpdr)
    {
        Console.WriteLine($"    transponder ({xpdr.Groups.Count} group(s)):");
        for (int g = 0; g < xpdr.Groups.Count; g++)
        {
            Console.Write($"      [{g}]:");
            foreach (var f in xpdr.Groups[g]) Console.Write($"  {f}");
            Console.WriteLine();
        }
    }

    // measurements
    if (seItem.Items["measurements"] is RepetitiveDecodedItem meas)
    {
        Console.WriteLine($"    measurements ({meas.Count} elements):");
        for (int i = 0; i < meas.Count; i++)
        {
            if (meas.Elements[i] is FixedDecodedItem elem)
            {
                Console.Write($"      [{i}]:");
                foreach (var f in elem.Fields) Console.Write($"  {f}");
                Console.WriteLine();
            }
        }
    }

    // nav_data
    if (seItem.Items["nav_data"] is CompoundDecodedItem nav)
    {
        Console.WriteLine($"    nav_data ({nav.Subitems.Count} subitem(s) present):");
        foreach (var (subId, sub) in nav.Subitems)
        {
            if (sub is FixedDecodedItem fi)
            {
                Console.Write($"      {subId}:");
                foreach (var f in fi.Fields) Console.Write($"  {f}");
                Console.WriteLine();
            }
        }
    }
}

bool rt100 = cat253Codec.RoundTrip(type100Encoded).SequenceEqual(type100Encoded);
Console.WriteLine($"  Round-trip: {(rt100 ? "PASS" : "FAIL")}");
Console.WriteLine();

// Demo 9: CAT048 – fixed + variable + Mode-3/A decode
// FSPEC 0xC8 = I048_010(FRN1) + I048_140(FRN2) + I048_070(FRN5), no extension
// I048_010: SAC=1, SIC=5
// I048_140: 12:00:00 UTC = 43200 s → 43200×128 = 5 529 600 = 0x549000
// I048_070: V=0 G=0 L=0 spare=0 Mode-3/A=7000₈=0x0E00
Console.WriteLine("--- Demo 9: CAT048 (fixed + Time of Day + Mode-3/A) ---");

string cat048Path = Path.Combine(schemasDir, "cat048.yml");
AsterixCodec cat048Codec = new AsterixCodecBuilder()
    .AddCategoryFromYaml(cat048Path)
    .WithMode(DecodeMode.Strict)
    .Build();

byte[] cat048Packet =
[
    0x30,                   // CAT = 48
    0x00, 0x0B,             // LEN = 11
    0xC8,                   // FSPEC: I048_010 | I048_140 | I048_070 (no FX)
    0x01, 0x05,             // I048_010: SAC=1, SIC=5
    0x54, 0x60, 0x00,       // I048_140: TOD = 12:00:00 UTC (5529600 × 1/128 s)
    0x0E, 0x00,             // I048_070: Mode-3/A = 7000₈ (no V/G/L flags)
];

AsterixPacket cat048Decoded = cat048Codec.Decode(cat048Packet);
DecodedRecord dr48 = cat048Decoded.Records[0];

PrintFixed(dr48, "I048_010");
PrintFixed(dr48, "I048_140");
PrintFixed(dr48, "I048_070");

bool rt9 = cat048Codec.RoundTrip(cat048Packet).SequenceEqual(cat048Packet);
Console.WriteLine($"  Full packet round-trip: {(rt9 ? "PASS" : "FAIL")}");
Console.WriteLine();

Console.WriteLine("=== All demos complete ===");


static void PrintFixed(DecodedRecord r, string itemId)
{
    if (!r.TryGet(itemId, out var item) || item is not FixedDecodedItem fixed_)
    {
        Console.WriteLine($"  {itemId}: (not present)");
        return;
    }

    Console.Write($"  {itemId}:");
    foreach (var f in fixed_.Fields)
        Console.Write($"  {f}");
    Console.WriteLine();
}

static void PrintCompound(DecodedRecord r, string itemId)
{
    if (!r.TryGet(itemId, out var item) || item is not CompoundDecodedItem compound)
    {
        Console.WriteLine($"  {itemId}: (not present)");
        return;
    }

    Console.WriteLine($"  {itemId} (compound):");
    foreach (var (subId, sub) in compound.Subitems)
    {
        if (sub is FixedDecodedItem fi)
        {
            Console.Write($"    {subId}:");
            foreach (var f in fi.Fields) Console.Write($"  {f}");
            Console.WriteLine();
        }
    }
}

static void PrintFspecRepetitive(DecodedRecord r, string itemId)
{
    if (!r.TryGet(itemId, out var item) || item is not FspecRepetitiveDecodedItem rep)
    {
        Console.WriteLine($"  {itemId}: (not present)");
        return;
    }

    Console.WriteLine($"  {itemId} (fspec_repetitive, {rep.Count} elements):");
    for (int i = 0; i < rep.Count; i++)
    {
        if (rep.Elements[i] is FixedDecodedItem elem)
        {
            Console.Write($"    [{i}]:");
            foreach (var f in elem.Fields) Console.Write($"  {f}");
            Console.WriteLine();
        }
    }
}

static void PrintRepetitive(DecodedRecord r, string itemId)
{
    if (!r.TryGet(itemId, out var item) || item is not RepetitiveDecodedItem rep)
    {
        Console.WriteLine($"  {itemId}: (not present)");
        return;
    }

    Console.WriteLine($"  {itemId} ({rep.Count} elements):");
    for (int i = 0; i < rep.Count; i++)
    {
        if (rep.Elements[i] is FixedDecodedItem elem)
        {
            Console.Write($"    [{i}]:");
            foreach (var f in elem.Fields) Console.Write($"  {f}");
            Console.WriteLine();
        }
    }
}