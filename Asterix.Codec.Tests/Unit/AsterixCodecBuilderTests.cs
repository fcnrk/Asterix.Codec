using Asterix.Codec.Decode;
using Asterix.Codec.Exceptions;
using Asterix.Codec.Schema;
using Asterix.Codec.Tests.Fixtures;
using FluentAssertions;

namespace Asterix.Codec.Tests.Unit;

/// <summary>
/// Tests for AsterixCodecBuilder guarding behaviour: double-build, no schemas, duplicates.
/// </summary>
public class AsterixCodecBuilderTests
{
    // ── Build() guards ────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithNoSchemas_ThrowsInvalidOperationException()
    {
        var builder = new AsterixCodecBuilder();

        var act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No schemas*");
    }

    [Fact]
    public void Build_CalledTwice_ThrowsInvalidOperationExceptionOnSecondCall()
    {
        var codec = new AsterixCodecBuilder()
            .AddCategory(SchemaFixtures.Cat062Schema())
            .Build();

        // codec was created; builder is now exhausted
        var builder = new AsterixCodecBuilder();
        builder.AddCategory(SchemaFixtures.Cat062Schema());
        builder.Build(); // first call OK

        var act = () => builder.Build(); // second call must throw
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Build() has already been called*");
    }

    [Fact]
    public void Build_WithValidSchema_ReturnsCodec()
    {
        var codec = new AsterixCodecBuilder()
            .AddCategory(SchemaFixtures.Cat062Schema())
            .Build();

        codec.Should().NotBeNull();
    }

    // ── Duplicate schema detection ────────────────────────────────────────────

    [Fact]
    public void AddCategory_DuplicateCategoryNumber_ThrowsInvalidOperationException()
    {
        var builder = new AsterixCodecBuilder();
        builder.AddCategory(SchemaFixtures.Cat062Schema()); // first: OK

        var act = () => builder.AddCategory(SchemaFixtures.Cat062Schema()); // second: must throw
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CAT062*already registered*");
    }

    [Fact]
    public void AddCategoryFromYaml_DuplicateCategoryNumber_ThrowsInvalidOperationException()
    {
        string cat062Path = Path.Combine(AppContext.BaseDirectory, "samples", "cat062.yml");

        var builder = new AsterixCodecBuilder();
        builder.AddCategoryFromYaml(cat062Path); // first: OK

        var act = () => builder.AddCategoryFromYaml(cat062Path); // second: must throw
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CAT062*already registered*");
    }

    // ── Fluent API ────────────────────────────────────────────────────────────

    [Fact]
    public void Builder_FluentChaining_Works()
    {
        var act = () => new AsterixCodecBuilder()
            .AddCategory(SchemaFixtures.Cat062Schema())
            .WithMode(DecodeMode.Lenient)
            .Build();

        act.Should().NotThrow();
    }

    // ── SchemaRegistry frozen after Build ─────────────────────────────────────

    [Fact]
    public void SchemaRegistry_FrozenAfterBuild_RejectsRegistration()
    {
        var registry = new SchemaRegistry();
        registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        registry.Freeze();

        var act = () => registry.RegisterCategory(SchemaFixtures.Cat062Schema());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*frozen*");
    }
}
