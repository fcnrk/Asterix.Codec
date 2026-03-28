using Asterix.Codec.Schema;
using Asterix.Codec.Schema.Models;
using FluentAssertions;

namespace Asterix.Codec.Tests.CustomSchema;

/// <summary>
/// Tests that cat253.yml and structured_explicit_cat253.yml are loaded into the correct runtime
/// schema objects.
/// </summary>
public class Cat253SchemaTests
{
    private static string SamplesPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "samples", file);

    // ── LoadCategory — cat253.yml ─────────────────────────────────────────────

    [Fact]
    public void LoadCategory_Cat253Yml_CategoryAndNameCorrect()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml"));

        schema.Category.Should().Be(253);
        schema.SchemaVersion.Should().Be(1);
        schema.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LoadCategory_Cat253Yml_DiscriminatorLoadedCorrectly()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml"));

        schema.MessageDiscriminator.Should().NotBeNull();
        schema.MessageDiscriminator!.ItemId.Should().Be("I253_010");
        schema.MessageDiscriminator.FieldName.Should().Be("message_type");
    }

    [Fact]
    public void LoadCategory_Cat253Yml_TwoMessagesWithCorrectDiscriminators()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml"));

        schema.Messages.Should().HaveCount(2);
        schema.Messages[0].Discriminator.Should().Be("1");
        schema.Messages[1].Discriminator.Should().Be("100");
    }

    [Fact]
    public void LoadCategory_Cat253Yml_MessageUapsCorrect()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml"));

        schema.Messages[0].Uap[0].Should().Be("I253_010");
        schema.Messages[0].Uap[1].Should().Be("I253_001");

        schema.Messages[1].Uap[0].Should().Be("I253_010");
        schema.Messages[1].Uap[1].Should().Be("I253_100");
    }

    [Fact]
    public void LoadCategory_Cat253Yml_I253_100_IsExplicitItemDefinition()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml"));

        schema.Items["I253_100"].Should().BeOfType<ExplicitItemDefinition>();
    }

    [Fact]
    public void LoadCategory_Cat253Yml_DiscriminatorItemIsFixed()
    {
        var schema = YamlSchemaLoader.LoadCategory(SamplesPath("cat253.yml"));

        schema.Items["I253_010"].Should().BeOfType<FixedItemDefinition>();
        var fixed010 = (FixedItemDefinition)schema.Items["I253_010"];
        fixed010.Length.Should().Be(1);
        fixed010.Fields[0].Name.Should().Be("message_type");
    }

    // ── LoadStructuredExplicitItemSet — structured_explicit_cat253.yml ───────────────────────

    [Fact]
    public void LoadStructuredExplicitItemSet_Cat253Yml_CategoryAndNameCorrect()
    {
        var schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        schema.Category.Should().Be(253);
        schema.SchemaVersion.Should().Be(1);
        schema.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LoadStructuredExplicitItemSet_Cat253Yml_OneItemDefined()
    {
        var schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        schema.Items.Should().ContainKey("I253_100");
        schema.Items.Should().HaveCount(1);
    }

    [Fact]
    public void LoadStructuredExplicitItemSet_Cat253Yml_FourContentEntries()
    {
        var schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        var i253_100 = schema.Items["I253_100"];
        i253_100.Content.Should().HaveCount(4);
    }

    [Fact]
    public void LoadStructuredExplicitItemSet_Cat253Yml_ContentEntryIdsCorrect()
    {
        var schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        var content = schema.Items["I253_100"].Content;
        content[0].Id.Should().Be("position");
        content[1].Id.Should().Be("transponder");
        content[2].Id.Should().Be("measurements");
        content[3].Id.Should().Be("nav_data");
    }

    [Fact]
    public void LoadStructuredExplicitItemSet_Cat253Yml_ContentEntryTypesCorrect()
    {
        var schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        var content = schema.Items["I253_100"].Content;
        content[0].Definition.Should().BeOfType<FixedItemDefinition>(); // position
        content[1].Definition.Should().BeOfType<VariableItemDefinition>(); // transponder
        content[2].Definition.Should().BeOfType<RepetitiveItemDefinition>(); // measurements
        content[3].Definition.Should().BeOfType<CompoundItemDefinition>(); // nav_data
    }

    [Fact]
    public void LoadStructuredExplicitItemSet_Cat253Yml_PositionFixed_SixBytes()
    {
        var schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        var position = (FixedItemDefinition)schema.Items["I253_100"].Content[0].Definition;
        position.Length.Should().Be(6);
        position.Fields.Should().HaveCount(3);
        position.Fields[0].Name.Should().Be("track_id");
    }

    [Fact]
    public void LoadStructuredExplicitItemSet_Cat253Yml_NavDataCompound_ThreeSubitems()
    {
        var schema = YamlSchemaLoader.LoadStructuredExplicitItemSet(SamplesPath("structured_explicit_cat253.yml"));

        var navData = (CompoundItemDefinition)schema.Items["I253_100"].Content[3].Definition;
        navData.Fspec.Should().HaveCount(3);
        navData.Subitems.Should().ContainKey("nav_data/ALT");
        navData.Subitems.Should().ContainKey("nav_data/SPD");
        navData.Subitems.Should().ContainKey("nav_data/HDG");
    }
}
