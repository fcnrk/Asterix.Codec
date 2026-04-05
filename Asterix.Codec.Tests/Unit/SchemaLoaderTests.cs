using Asterix.Codec.Exceptions;
using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

/// <summary>
/// Tests for YamlSchemaLoader — verifies that the real YAML sample files are loaded
/// into the correct runtime schema objects and that error paths throw the right exceptions.
/// </summary>
public class SchemaLoaderTests
{
    private static string SamplesPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "samples", file);

    // ── LoadCategory — cat062.yml ─────────────────────────────────────────────

    [Fact]
    public void LoadCategory_Cat062Yml_CategoryAndNameCorrect()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        schema.Category.Should().Be(62);
        schema.Name.Should().Be("System Track Data");
        schema.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void LoadCategory_Cat062Yml_ItemCountCorrect()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        // 14 items: I062_010..I062_380 + I062_510 + SP
        schema.Items.Should().HaveCount(14);
    }

    [Fact]
    public void LoadCategory_Cat062Yml_UapOrderCorrect()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));
        var uap = schema.Messages[0].Uap;

        uap[0].Should().Be("I062_010");
        uap[2].Should().Be("I062_040");
        uap[11].Should().Be("I062_290");
    }

    [Fact]
    public void LoadCategory_Cat062Yml_FixedItemHasCorrectLength()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i010 = schema.Items["I062_010"].Should().BeOfType<FixedItemDefinition>().Subject;
        i010.Length.Should().Be(2);
    }

    [Fact]
    public void LoadCategory_Cat062Yml_FixedItemFieldsCorrect()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i010 = (FixedItemDefinition)schema.Items["I062_010"];
        i010.Fields.Should().HaveCount(2);
        i010.Fields[0].Name.Should().Be("sac");
        i010.Fields[0].Bits.Should().Be(8);
        i010.Fields[0].BitOffset.Should().Be(0);
        i010.Fields[1].Name.Should().Be("sic");
        i010.Fields[1].BitOffset.Should().Be(8);
    }

    [Fact]
    public void LoadCategory_Cat062Yml_I062_060_Mode3a_BitOffsetIsFour()
    {
        // mode3a has a 1-bit spare at position 3; YAML must declare bit: 4
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i060 = (FixedItemDefinition)schema.Items["I062_060"];
        var mode3a = i060.Fields.Single(f => f.Name == "mode3a");
        mode3a.BitOffset.Should().Be(4, "there is a 1-bit spare between 'l' (offset 2) and mode3a");
        mode3a.Bits.Should().Be(12);
    }

    [Fact]
    public void LoadCategory_Cat062Yml_I062_070_ScaleIs1Over128()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i070 = (FixedItemDefinition)schema.Items["I062_070"];
        var time = i070.Fields[0];
        time.Scale.Should().NotBeNull();
        time.Scale!.Value.Numerator.Should().Be(1);
        time.Scale!.Value.Denominator.Should().Be(128);
    }

    [Fact]
    public void LoadCategory_Cat062Yml_I062_245_StringFieldIsIa5()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i245 = (FixedItemDefinition)schema.Items["I062_245"];
        var callsign = i245.Fields[0];
        callsign.Type.Should().Be(FieldType.String);
        callsign.Encoding.Should().Be(StringEncoding.Ia5);
        callsign.StringLength.Should().Be(6);
    }

    [Fact]
    public void LoadCategory_Cat062Yml_I062_210_IsCompound()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i210 = schema.Items["I062_210"].Should().BeOfType<CompoundItemDefinition>().Subject;
        i210.Fspec.Should().HaveCount(6);
        i210.Subitems.Should().ContainKey("qx");
        i210.Subitems.Should().ContainKey("qvy");
    }

    [Fact]
    public void LoadCategory_Cat062Yml_I062_290_IsRepetitive()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i290 = schema.Items["I062_290"].Should().BeOfType<RepetitiveItemDefinition>().Subject;
        i290.CountField.Bits.Should().Be(8);
        i290.Element.Should().BeOfType<FixedItemDefinition>();
    }

    [Fact]
    public void LoadCategory_Cat062Yml_I062_105_IntFieldsWithScale()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        var i105 = (FixedItemDefinition)schema.Items["I062_105"];
        i105.Fields[0].Type.Should().Be(FieldType.Int);
        i105.Fields[0].Scale!.Value.Numerator.Should().Be(180);
        i105.Fields[1].BitOffset.Should().Be(32);
    }

    [Fact]
    public void LoadCategory_StreamOverload_SameResultAsFilePath()
    {
        var fromFile = YamlSchemaLoader.LoadCategory(SamplesPath("cat062.yml"));

        AsterixCategorySchema fromStream;
        using (var stream = File.OpenRead(SamplesPath("cat062.yml")))
            fromStream = YamlSchemaLoader.LoadCategory(stream);

        fromStream.Category.Should().Be(fromFile.Category);
        fromStream.Items.Count.Should().Be(fromFile.Items.Count);
    }

    // ── LoadCategory — error paths ────────────────────────────────────────────

    [Fact]
    public void LoadCategory_UnsupportedVersion_ThrowsUnsupportedSchemaVersionException()
    {
        const string yaml = "schema_version: 99\ncategory: 62\nname: test\nmessages: []\nitems: {}";
        using var stream = ToStream(yaml);

        Assert.Throws<UnsupportedSchemaVersionException>(() =>
            YamlSchemaLoader.LoadCategory(stream, "test"));
    }

    [Fact]
    public void LoadCategory_MalformedYaml_ThrowsSchemaLoadException()
    {
        const string yaml = "schema_version: [\n  broken yaml {{{";
        using var stream = ToStream(yaml);

        Assert.Throws<SchemaLoadException>(() =>
            YamlSchemaLoader.LoadCategory(stream, "test"));
    }

    // ── LoadSpfFieldSet — spf_custom_062.yml ──────────────────────────────────

    [Fact]
    public void LoadSpfFieldSet_SpfCustom062_StructureCountCorrect()
    {
        var schema = YamlSchemaLoader.LoadSpfFieldSet(SamplesPath("spf_custom_062.yml"));

        var def = schema.FieldSets["SPF_CUSTOM_062"];
        // length, f1RecordCount, f1(repetitive), presence(dynamic), f4..f8(5 optional) = 9
        def.Structure.Should().HaveCount(9);
    }

    [Fact]
    public void LoadSpfFieldSet_SpfCustom062_ScalarEntriesCorrect()
    {
        var schema = YamlSchemaLoader.LoadSpfFieldSet(SamplesPath("spf_custom_062.yml"));
        var structure = schema.FieldSets["SPF_CUSTOM_062"].Structure;

        var length = structure[0].Should().BeOfType<ScalarEntry>().Subject;
        length.Name.Should().Be("length");
        length.Bits.Should().Be(16);

        var count = structure[1].Should().BeOfType<ScalarEntry>().Subject;
        count.Name.Should().Be("f1RecordCount");
        count.Bits.Should().Be(8);
    }

    [Fact]
    public void LoadSpfFieldSet_SpfCustom062_RepetitiveEntryCountRefIsF1RecordCount()
    {
        var schema = YamlSchemaLoader.LoadSpfFieldSet(SamplesPath("spf_custom_062.yml"));
        var structure = schema.FieldSets["SPF_CUSTOM_062"].Structure;

        var rep = structure[2].Should().BeOfType<SpfRepetitiveEntry>().Subject;
        rep.CountRef.Should().Be("f1RecordCount");
        rep.Element.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void LoadSpfFieldSet_SpfCustom062_DynamicPresenceHasFiveFields()
    {
        var schema = YamlSchemaLoader.LoadSpfFieldSet(SamplesPath("spf_custom_062.yml"));
        var structure = schema.FieldSets["SPF_CUSTOM_062"].Structure;

        var presence = structure[3].Should().BeOfType<DynamicPresenceEntry>().Subject;
        presence.BitWidth.Should().Be(8);
        presence.Fields.Should().HaveCount(5);
        presence.Fields.Should().Contain("f4");
        presence.Fields.Should().Contain("f8");
    }

    [Fact]
    public void LoadSpfFieldSet_SpfCustom062_OptionalF4PresenceRefCorrect()
    {
        var schema = YamlSchemaLoader.LoadSpfFieldSet(SamplesPath("spf_custom_062.yml"));
        var structure = schema.FieldSets["SPF_CUSTOM_062"].Structure;

        var f4 = structure[4].Should().BeOfType<OptionalEntry>().Subject;
        f4.Name.Should().Be("f4");
        f4.PresenceGroup.Should().Be("presence");
        f4.PresenceField.Should().Be("f4");
        f4.Field.Type.Should().Be(FieldType.UInt);
        f4.Field.Bits.Should().Be(8);
    }

    [Fact]
    public void LoadSpfFieldSet_SpfCustom062_OptionalF8IsAsciiString()
    {
        var schema = YamlSchemaLoader.LoadSpfFieldSet(SamplesPath("spf_custom_062.yml"));
        var structure = schema.FieldSets["SPF_CUSTOM_062"].Structure;

        var f8 = structure[8].Should().BeOfType<OptionalEntry>().Subject;
        f8.Field.Type.Should().Be(FieldType.String);
        f8.Field.Encoding.Should().Be(StringEncoding.Ascii);
        f8.Field.StringLength.Should().Be(4);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Stream ToStream(string yaml)
    {
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        return ms;
    }
}
