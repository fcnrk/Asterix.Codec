namespace Asterix.Codec.Tests.Fixtures;

/// <summary>
/// Hand-crafted binary payloads for integration and round-trip tests.
///
/// All byte values are documented with their expected decoded field values so
/// that any regression is immediately traceable to a specific field.
///
/// Layout matches the schemas in <see cref="SchemaFixtures"/>.
/// </summary>
internal static class PayloadFixtures
{
    // ── CAT062 simple: I062_010 + I062_040 ───────────────────────────────────
    //
    // FSPEC:          0xA0  (1010 0000: bit7=I062_010, bit5=I062_040, FX=0)
    // I062_010:       0x01, 0x02        → SAC=1, SIC=2
    // I062_040:       0x12, 0x34        → track_number=0x1234=4660
    //
    // Header: CAT=0x3E(62), LEN=0x00 0x08 (8 bytes total)
    public static readonly byte[] Cat062Simple =
    [
        0x3E, 0x00, 0x08,   // header: CAT=62, LEN=8
        0xA0,               // FSPEC
        0x01, 0x02,         // I062_010: SAC=1, SIC=2
        0x12, 0x34,         // I062_040: track_number=4660
    ];

    // ── CAT062 with time: I062_010 + I062_040 + I062_070 ────────────────────
    //
    // FSPEC:          0xA8  (1010 1000: bit7=010, bit5=040, bit3=070, FX=0)
    // I062_070:       0x00, 0x25, 0x80  → raw=9600, scaled=9600/128=75.0 seconds
    //
    // Header: CAT=0x3E, LEN=0x00 0x0B (11 bytes)
    public static readonly byte[] Cat062WithTime =
    [
        0x3E, 0x00, 0x0B,   // header
        0xA8,               // FSPEC
        0x01, 0x02,         // I062_010
        0x12, 0x34,         // I062_040
        0x00, 0x25, 0x80,   // I062_070: 9600 raw → 75.0 s
    ];

    // ── CAT062 with IA5 callsign: I062_010 + I062_245 ────────────────────────
    //
    // FSPEC byte 0:   0x81  (1000 0001: bit7=I062_010, FX=1 → second byte follows)
    // FSPEC byte 1:   0x20  (0010 0000: bit5=I062_245, FX=0)
    // I062_245:       IA5("BAW123") = 0x08,0x15,0xF1,0xCB,0x38,0x20
    //
    // IA5 codes: B=2, A=1, W=23, 1=49, 2=50, 3=51, ' '=32, ' '=32
    // Packed 6-bit MSB-first → 0x08,0x15,0xF1,0xCB,0x38,0x20
    //
    // Header: CAT=0x3E, LEN=0x00 0x0D (13 bytes)
    public static readonly byte[] Cat062WithCallsign =
    [
        0x3E, 0x00, 0x0D,               // header
        0x81, 0x20,                     // FSPEC (2 bytes)
        0x01, 0x02,                     // I062_010
        0x08, 0x15, 0xF1, 0xCB, 0x38, 0x20,  // I062_245: "BAW123"
    ];

    // ── CAT062 with compound: I062_010 + I062_210 (qx + qy present) ─────────
    //
    // FSPEC byte 0:   0x81  (bit7=I062_010, FX=1)
    // FSPEC byte 1:   0x40  (bit6=I062_210, FX=0)
    // I062_210 inner FSPEC: 0xC0 (1100 0000: bit7=qx, bit6=qy)
    // qx:             0x04  → value=4, scaled=1.0
    // qy:             0x08  → value=8, scaled=2.0
    //
    // Header: CAT=0x3E, LEN=0x00 0x0A (10 bytes)
    public static readonly byte[] Cat062WithCompound =
    [
        0x3E, 0x00, 0x0A,   // header
        0x81, 0x40,         // FSPEC (2 bytes)
        0x01, 0x02,         // I062_010
        0xC0, 0x04, 0x08,   // I062_210: inner FSPEC + qx + qy
    ];

    // ── CAT062 with repetitive: I062_010 + I062_290 (2 elements) ────────────
    //
    // FSPEC byte 0:   0x81  (bit7=I062_010, FX=1)
    // FSPEC byte 1:   0x08  (bit3=I062_290, FX=0)
    // count:          0x02  → 2 elements
    // element[0]:     0x01, 0x00  → age raw=256, scaled=256/128=2.0
    // element[1]:     0x02, 0x00  → age raw=512, scaled=512/128=4.0
    //
    // Header: CAT=0x3E, LEN=0x00 0x0C (12 bytes)
    public static readonly byte[] Cat062WithRepetitive =
    [
        0x3E, 0x00, 0x0C,           // header
        0x81, 0x08,                 // FSPEC (2 bytes)
        0x01, 0x02,                 // I062_010
        0x02, 0x01, 0x00, 0x02, 0x00,  // I062_290: count=2, [256, 512]
    ];

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

    // ── SPF_CUSTOM_062 raw block (not wrapped in CAT packet) ─────────────────
    //
    // Structure:
    //   length (uint16):       0x00,0x13 = 19 bytes total
    //   f1RecordCount (uint8): 0x02 = 2 elements
    //   f1[0]:                 f2=0x0A(10), f3=0x0B(11)
    //   f1[1]:                 f2=0x0C(12), f3=0x0D(13)
    //   presence.f4:           0x01 (present)
    //   presence.f5:           0x00 (absent)
    //   presence.f6:           0x01 (present)
    //   presence.f7:           0x00 (absent)
    //   presence.f8:           0x01 (present)
    //   f4 (uint8):            0x42 = 66
    //   f5:                    (absent)
    //   f6 (uint16):           0x12,0x34 = 4660
    //   f7:                    (absent)
    //   f8 (string ascii 4):   "TEST" = 0x54,0x45,0x53,0x54
    //
    // Byte count: 2+1+4+5+1+2+4 = 19 ✓
    public static readonly byte[] SpfCustom062Block =
    [
        0x00, 0x13,         // length = 19
        0x02,               // f1RecordCount = 2
        0x0A, 0x0B,         // f1[0]: f2=10, f3=11
        0x0C, 0x0D,         // f1[1]: f2=12, f3=13
        0x01,               // presence.f4 = 1 (present)
        0x00,               // presence.f5 = 0 (absent)
        0x01,               // presence.f6 = 1 (present)
        0x00,               // presence.f7 = 0 (absent)
        0x01,               // presence.f8 = 1 (present)
        0x42,               // f4 = 66
        0x12, 0x34,         // f6 = 4660
        0x54, 0x45, 0x53, 0x54,  // f8 = "TEST"
    ];

    // ── CAT253 Type 001 — status record ──────────────────────────────────────
    //
    // FSPEC:       0xC0  (1100 0000: bit7=I253_010, bit6=I253_001, FX=0)
    // I253_010:    0x01  → message_type=1
    // I253_001:    0x00, 0x2A → status=42
    //
    // Header: CAT=0xFD(253), LEN=0x00 0x07 (7 bytes total)
    public static readonly byte[] Cat253Type001 =
    [
        0xFD, 0x00, 0x07,   // header: CAT=253, LEN=7
        0xC0,               // FSPEC: I253_010(1), I253_001(1)
        0x01,               // I253_010: message_type=1
        0x00, 0x2A,         // I253_001: status=42
    ];

    // ── CAT253 Type 100 — structured-explicit application data ───────────
    //
    // FSPEC:       0xC0  (bit7=I253_010, bit6=I253_100, FX=0)
    // I253_010:    0x64  → message_type=100
    // I253_100:    structured-explicit container, LEN=20 (1 LEN byte + 19 content bytes)
    //
    // Inner content (19 bytes, decoded sequentially by structured-explicit schema):
    //   position (fixed, 6B):
    //     track_id   = 0x0007 → 7
    //     latitude   = 0x0100 → raw=256, scaled=256×(180/65536)≈0.703°
    //     longitude  = 0x0200 → raw=512, scaled=512×(360/65536)≈2.813°
    //   transponder (variable, 1 octet, fx=0):
    //     alert=0, spi=0, squawk=5, spare=0, fx=0 → 0x14
    //   measurements (repetitive, count=2, 3B/element):
    //     [0]: sensor_id=1, quality=100, range=50
    //     [1]: sensor_id=2, quality=80,  range=30
    //   nav_data (compound, FSPEC=0xC0, ALT+SPD present):
    //     ALT: raw=4000 → scaled=4000×0.25=1000.0
    //     SPD: raw=25000 → scaled=25000×0.01=250.0
    //
    // Header: CAT=0xFD, LEN=0x00 0x19 (25 bytes total)
    public static readonly byte[] Cat253Type100 =
    [
        0xFD, 0x00, 0x19,   // header: CAT=253, LEN=25
        0xC0,               // FSPEC: I253_010(1), I253_100(1)
        0x64,               // I253_010: message_type=100
        0x14,               // I253_100 LEN=20
        0x00, 0x07,         // position.track_id=7
        0x01, 0x00,         // position.latitude=256 (raw)
        0x02, 0x00,         // position.longitude=512 (raw)
        0x14,               // transponder: alert=0, spi=0, squawk=5, spare=0, fx=0
        0x02,               // measurements.count=2
        0x01, 0x64, 0x32,   // measurements[0]: sensor_id=1, quality=100, range=50
        0x02, 0x50, 0x1E,   // measurements[1]: sensor_id=2, quality=80, range=30
        0xC0,               // nav_data compound FSPEC: ALT(1), SPD(1), HDG(0)
        0x0F, 0xA0,         // nav_data.ALT: raw=4000 → 1000.0 m
        0x61, 0xA8,         // nav_data.SPD: raw=25000 → 250.0 kt
    ];

    // ── Negative: truncated packet ────────────────────────────────────────────
    // Only the 3-byte header; no record bytes.
    public static readonly byte[] TruncatedHeader = [0x3E, 0x00, 0x03];

    // ── Negative: LEN > data.Length ──────────────────────────────────────────
    public static readonly byte[] LenExceedsData = [0x3E, 0x00, 0xFF, 0xA0];
}
