using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Serialization;

/// <summary>Covers the stored preset and the two ways a union reaches it — attributes on the base, or a registry declared outside it.</summary>
public sealed class StoredJsonTests
{
    private enum Shade
    {
        DarkRed,
    }

    private sealed record Swatch
    {
        public required Shade Shade { get; init; }

        public required string? Note { get; init; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(Circle), "circle")]
    private abstract record Shape;

    private sealed record Circle : Shape
    {
        public required double Radius { get; init; }
    }

    private enum PetKind
    {
        Cat,
        Dog,
    }

    private abstract record Pet;

    private sealed record Cat : Pet;

    private sealed record Dog : Pet
    {
        public required string Name { get; init; }
    }

    [Fact]
    public void Default_ShouldWriteEnumsAsCamelCaseStringsAndKeepNulls()
    {
        var json = JsonSerializer.Serialize(new Swatch { Shade = Shade.DarkRed, Note = null }, StoredJsonConstants.Default);

        json.Should().Be("""{"shade":"darkRed","note":null}""");
    }

    [Fact]
    public void Default_ShouldRoundTripARequiredNullableMember()
    {
        var restored = JsonSerializer.Deserialize<Swatch>("""{"shade":"darkRed","note":null}""", StoredJsonConstants.Default);

        restored.Should().NotBeNull();
        restored!.Note.Should().BeNull();
    }

    [Fact]
    public void Default_ShouldReadAnAttributeUnion_WhenTheTypeKeyComesLast()
    {
        var restored = JsonSerializer.Deserialize<Shape>("""{"radius":2,"type":"circle"}""", StoredJsonConstants.Default);

        restored.Should().BeOfType<Circle>().Which.Radius.Should().Be(2);
    }

    [Fact]
    public void Factory_ShouldBindAUnionDeclaredOutsideTheType()
    {
        var registry = new SubtypeRegistry<Pet, PetKind>((PetKind.Cat, typeof(Cat)), (PetKind.Dog, typeof(Dog)));
        var options = StoredJsonOptionsFactory.Create(registry.ToJsonModifier());

        var json = JsonSerializer.Serialize<Pet>(new Dog { Name = "Rex" }, options);
        var restored = JsonSerializer.Deserialize<Pet>(json, options);

        json.Should().Contain("\"type\":\"dog\"");
        restored.Should().BeOfType<Dog>().Which.Name.Should().Be("Rex");
    }

    [Fact]
    public void KeyedProfile_ShouldResolveThePinnedOptionsInstance()
    {
        var registry = new SubtypeRegistry<Pet, PetKind>((PetKind.Cat, typeof(Cat)), (PetKind.Dog, typeof(Dog)));
        var options = StoredJsonOptionsFactory.Create(registry.ToJsonModifier());
        var provider = new ServiceCollection()
            .AddStoredJsonOptionsProfile("pets", options)
            .BuildServiceProvider();

        var resolved = provider.GetRequiredStoredJsonOptionsProfile("pets");

        resolved.Should().BeSameAs(options);
        JsonSerializer.Deserialize<Pet>(
                JsonSerializer.Serialize<Pet>(new Dog { Name = "Rex" }, resolved),
                resolved)
            .Should().BeOfType<Dog>()
            .Which.Name.Should().Be("Rex");
    }

    [Fact]
    public void KeyedProfile_ShouldCreateOptionsFromModifiers()
    {
        var registry = new SubtypeRegistry<Pet, PetKind>((PetKind.Cat, typeof(Cat)), (PetKind.Dog, typeof(Dog)));
        var provider = new ServiceCollection()
            .AddStoredJsonOptionsProfile("pets", registry.ToJsonModifier())
            .BuildServiceProvider();

        var options = provider.GetRequiredStoredJsonOptionsProfile("pets");

        JsonSerializer.Serialize<Pet>(new Cat(), options).Should().Contain("\"type\":\"cat\"");
    }

    [Fact]
    public void KeyedProfile_ShouldRejectMutableOptions()
    {
        var services = new ServiceCollection();

        var act = () => services.AddStoredJsonOptionsProfile("mutable", new JsonSerializerOptions());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void KeyedProfile_ShouldFailExplicitly_WhenKeyIsUnknown()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var act = () => provider.GetRequiredStoredJsonOptionsProfile("missing");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing*");
    }
}
