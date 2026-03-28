using Asterix.Codec.Exceptions;
using Asterix.Codec.Schema;
using FluentAssertions;

namespace Asterix.Codec.Tests.Negative;

/// <summary>
/// Negative tests for YamlSchemaLoader — verifies that malformed YAML, unsupported
/// versions, and invalid cross-references are rejected with the correct exception types.
/// </summary>
public class InvalidYamlTests
{
    private static Stream ToStream(string yaml) =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));

    // ── Category negative cases ───────────────────────────────────────────────

    [Fact]
    public void LoadCategory_WrongSchemaVersion_ThrowsUnsupportedSchemaVersionException()
    {
        const string yaml =
            "schema_version: 5\ncategory: 62\nname: test\nmessages: []\nitems: {}";
        using var stream = ToStream(yaml);

        var ex = Assert.Throws<UnsupportedSchemaVersionException>(() =>
            YamlSchemaLoader.LoadCategory(stream, "test.yml"));

        ex.Version.Should().Be(5);
    }

    [Fact]
    public void LoadCategory_EmptyStream_ThrowsSchemaLoadException()
    {
        using var stream = ToStream("");

        Assert.Throws<SchemaLoadException>(() =>
            YamlSchemaLoader.LoadCategory(stream, "empty.yml"));
    }

    [Fact]
    public void LoadCategory_MalformedYaml_ThrowsSchemaLoadException()
    {
        const string yaml = ": bad:\n  - [unclosed";
        using var stream = ToStream(yaml);

        Assert.Throws<SchemaLoadException>(() =>
            YamlSchemaLoader.LoadCategory(stream, "bad.yml"));
    }

    [Fact]
    public void LoadCategory_UapRefMissing_ThrowsSchemaValidationException()
    {
        const string yaml = """
            schema_version: 1
            category: 99
            name: test
            messages:
              - id: default
                name: test
                discriminator: null
                uap:
                  - DOES_NOT_EXIST
            items: {}
            """;
        using var stream = ToStream(yaml);

        Assert.Throws<SchemaValidationException>(() =>
            YamlSchemaLoader.LoadCategory(stream, "bad.yml"));
    }

    // ── SPF negative cases ────────────────────────────────────────────────────

    [Fact]
    public void LoadSpfFieldSet_WrongSchemaVersion_ThrowsUnsupportedSchemaVersionException()
    {
        const string yaml = "schema_version: 2\nspf_field_sets: {}";
        using var stream = ToStream(yaml);

        Assert.Throws<UnsupportedSchemaVersionException>(() =>
            YamlSchemaLoader.LoadSpfFieldSet(stream, "bad.yml"));
    }

    [Fact]
    public void LoadSpfFieldSet_CountRefForwardRef_ThrowsSchemaValidationException()
    {
        const string yaml = """
            schema_version: 1
            spf_field_sets:
              BAD_SPF:
                type: spf
                description: test
                structure:
                  - name: rep
                    type: repetitive
                    count_ref: cnt
                    element:
                      type: group
                      fields: []
                  - name: cnt
                    type: uint
                    bits: 8
            """;
        using var stream = ToStream(yaml);

        Assert.Throws<SchemaValidationException>(() =>
            YamlSchemaLoader.LoadSpfFieldSet(stream, "bad.yml"));
    }

    [Fact]
    public void LoadSpfFieldSet_PresentIfBadGroup_ThrowsSchemaValidationException()
    {
        const string yaml = """
            schema_version: 1
            spf_field_sets:
              BAD_SPF:
                type: spf
                description: test
                structure:
                  - name: f1
                    type: optional
                    present_if: no_such_group.f1
                    field:
                      type: uint
                      bits: 8
            """;
        using var stream = ToStream(yaml);

        Assert.Throws<SchemaValidationException>(() =>
            YamlSchemaLoader.LoadSpfFieldSet(stream, "bad.yml"));
    }
}
