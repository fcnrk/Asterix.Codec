using Asterix.Codec.Schema.Models;

namespace Asterix.Codec.Tests.Fixtures;

/// <summary>
/// Programmatically constructed schema objects that mirror the YAML samples.
/// Used by all tests that need a schema but cannot yet use YamlSchemaLoader (Phase 3).
///
/// Every definition here must stay byte-for-byte consistent with the binary payloads
/// in <see cref="PayloadFixtures"/>. If you change a field layout here, update the
/// corresponding bytes there.
/// </summary>
internal static class SchemaFixtures
{
    // ── CAT062 items ──────────────────────────────────────────────────────────

    public static FixedItemDefinition I062_010() => new(2,
    [
        new("sac", FieldType.UInt, 8, bitOffset: 0),
        new("sic", FieldType.UInt, 8, bitOffset: 8),
    ]);

    public static FixedItemDefinition I062_015() => new(1,
    [
        new("service_id", FieldType.UInt, 8, bitOffset: 0),
    ]);

    public static FixedItemDefinition I062_040() => new(2,
    [
        new("track_number", FieldType.UInt, 16, bitOffset: 0),
    ]);

    /// <summary>I062/060 — Mode 3/A code with 3 leading bool flags and a 12-bit code.</summary>
    public static FixedItemDefinition I062_060() => new(2,
    [
        new("v",      FieldType.Bool, 1, bitOffset: 0),
        new("g",      FieldType.Bool, 1, bitOffset: 1),
        new("l",      FieldType.Bool, 1, bitOffset: 2),
        new("mode3a", FieldType.UInt, 12, bitOffset: 4), // 1 spare bit between l and mode3a
    ]);

    public static FixedItemDefinition I062_070() => new(3,
    [
        new("time", FieldType.UInt, 24, bitOffset: 0,
            scale: new ScaleFactor(1, 128)),
    ]);

    public static FixedItemDefinition I062_105() => new(8,
    [
        new("latitude",  FieldType.Int, 32, bitOffset: 0,
            scale: new ScaleFactor(180, 2147483648)),
        new("longitude", FieldType.Int, 32, bitOffset: 32,
            scale: new ScaleFactor(180, 2147483648)),
    ]);

    public static FixedItemDefinition I062_100() => new(4,
    [
        new("vx", FieldType.Int, 16, bitOffset: 0,  scale: new ScaleFactor(1, 4)),
        new("vy", FieldType.Int, 16, bitOffset: 16, scale: new ScaleFactor(1, 4)),
    ]);

    public static FixedItemDefinition I062_185() => new(2,
    [
        new("ax", FieldType.Int, 8, bitOffset: 0,  scale: new ScaleFactor(1, 4)),
        new("ay", FieldType.Int, 8, bitOffset: 8,  scale: new ScaleFactor(1, 4)),
    ]);

    public static FixedItemDefinition I062_245() => new(6,
    [
        new("callsign", FieldType.String, bits: 48, bitOffset: 0,
            encoding: StringEncoding.Ia5, stringLength: 6),
    ]);

    public static CompoundItemDefinition I062_210() => new(
        fspec: ["qx", "qy", "qvx", "qvy", "qax", "qay"],
        subitems: new Dictionary<string, ItemDefinition>
        {
            ["qx"]  = new FixedItemDefinition(1, [new("value", FieldType.UInt, 8, 0, new ScaleFactor(1, 4))]),
            ["qy"]  = new FixedItemDefinition(1, [new("value", FieldType.UInt, 8, 0, new ScaleFactor(1, 4))]),
            ["qvx"] = new FixedItemDefinition(1, [new("value", FieldType.UInt, 8, 0, new ScaleFactor(1, 4))]),
            ["qvy"] = new FixedItemDefinition(1, [new("value", FieldType.UInt, 8, 0, new ScaleFactor(1, 4))]),
            ["qax"] = new FixedItemDefinition(1, [new("value", FieldType.UInt, 8, 0)]),
            ["qay"] = new FixedItemDefinition(1, [new("value", FieldType.UInt, 8, 0)]),
        });

    public static RepetitiveItemDefinition I062_290() => new(
        countField: new CountFieldDefinition(8),
        element: new FixedItemDefinition(2, [
            new("age", FieldType.UInt, 16, bitOffset: 0, scale: new ScaleFactor(1, 128)),
        ]));

    public static FspecRepetitiveItemDefinition I062_510() => new(
        new FixedItemDefinition(4,
        [
            new FieldDefinition("sac",          FieldType.UInt, 8,  bitOffset: 0),
            new FieldDefinition("sic",          FieldType.UInt, 8,  bitOffset: 8),
            new FieldDefinition("track_number", FieldType.UInt, 16, bitOffset: 16),
        ]));

    public static CompoundItemDefinition I062_380() => new(
        fspec: ["adr", "id", "mhg", "ias", "tas", "sal"],
        subitems: new Dictionary<string, ItemDefinition>
        {
            ["adr"] = new FixedItemDefinition(3, [new("address",  FieldType.UInt, 24, 0)]),
            ["id"]  = new FixedItemDefinition(6, [new("callsign", FieldType.String, 48, 0, encoding: StringEncoding.Ia5, stringLength: 6)]),
            ["mhg"] = new FixedItemDefinition(2, [new("heading",  FieldType.UInt, 16, 0, new ScaleFactor(360, 65536))]),
            ["ias"] = new FixedItemDefinition(2, [new("indicated_airspeed", FieldType.UInt, 16, 0)]),
            ["tas"] = new FixedItemDefinition(2, [new("true_airspeed",      FieldType.UInt, 16, 0)]),
            ["sal"] = new FixedItemDefinition(2, [new("selected_altitude",  FieldType.Int,  16, 0, new ScaleFactor(25, 1))]),
        });

    // ── CAT062 category schema ────────────────────────────────────────────────

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

    // ── SPF_CUSTOM_062 ────────────────────────────────────────────────────────

    public static SpfFieldSetDefinition SpfCustom062() => new(
        name: "SPF_CUSTOM_062",
        description: "Custom SPF field set for CAT062 with dynamic presence and repetitive F1 structure",
        structure:
        [
            new ScalarEntry("length",        FieldType.UInt, bits: 16),
            new ScalarEntry("f1RecordCount", FieldType.UInt, bits: 8),
            new SpfRepetitiveEntry("f1", countRef: "f1RecordCount",
                element: new SpfElementDefinition([
                    new("f2", FieldType.UInt, 8, bitOffset: 0),
                    new("f3", FieldType.UInt, 8, bitOffset: 8),
                ])),
            new DynamicPresenceEntry("presence", bitWidth: 8,
                fields: ["f4", "f5", "f6", "f7", "f8"]),
            new OptionalEntry("f4", presenceGroup: "presence", presenceField: "f4",
                field: new FieldDefinition("f4", FieldType.UInt, 8, 0)),
            new OptionalEntry("f5", presenceGroup: "presence", presenceField: "f5",
                field: new FieldDefinition("f5", FieldType.UInt, 8, 0)),
            new OptionalEntry("f6", presenceGroup: "presence", presenceField: "f6",
                field: new FieldDefinition("f6", FieldType.UInt, 16, 0)),
            new OptionalEntry("f7", presenceGroup: "presence", presenceField: "f7",
                field: new FieldDefinition("f7", FieldType.UInt, 32, 0)),
            new OptionalEntry("f8", presenceGroup: "presence", presenceField: "f8",
                field: new FieldDefinition("f8", FieldType.String, bits: 32, bitOffset: 0,
                    encoding: StringEncoding.Ascii, stringLength: 4)),
        ]);
}
