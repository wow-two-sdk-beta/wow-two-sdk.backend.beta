using System.Text.Json.Serialization;
using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Serialization;

public sealed class SubtypeRegistryTests
{
    [Fact]
    public void Discriminator_UsesDecodedJsonTokenAndExposesReadOnlyEntries()
    {
        var registry = new SubtypeRegistry<Pet, EscapedKind>((EscapedKind.Cat, typeof(Cat)));
        Assert.Equal("cat\"é", registry.Subtypes[0].Discriminator);
        Assert.Throws<NotSupportedException>(() => ((IList<(EscapedKind, Type, string)>)registry.Subtypes)[0] = default);
    }

    [Fact]
    public void Construction_RejectsUndefinedKindsAndNullTypes()
    {
        Assert.Throws<ArgumentException>(() => new SubtypeRegistry<Pet, PetKind>(((PetKind)99, typeof(Cat))));
        Assert.Throws<ArgumentException>(() => new SubtypeRegistry<Pet, PetKind>((PetKind.Cat, null!)));
        Assert.Throws<ArgumentException>(() => new SubtypeRegistry<Pet, DuplicateKind>((DuplicateKind.Cat, typeof(Cat)), (DuplicateKind.Dog, typeof(Dog))));
    }

    private enum EscapedKind { [JsonStringEnumMemberName("cat\"é")] Cat }
    private enum DuplicateKind { [JsonStringEnumMemberName("same")] Cat, [JsonStringEnumMemberName("same")] Dog }

    private enum PetKind
    {
        Cat,
        Dog,
    }

    private abstract record Pet;

    private sealed record Cat : Pet;

    private sealed record Dog : Pet;

    [JsonDerivedType(typeof(Square), "box")]
    private abstract record Shape;

    private sealed record Square : Shape;

    private enum ShapeKind
    {
        Square,
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenAMemberIsUnbound()
    {
        var act = () => new SubtypeRegistry<Pet, PetKind>((PetKind.Cat, typeof(Cat)));

        act.Should().Throw<ArgumentException>().WithMessage("*Dog*");
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenTheAttributesDisagree()
    {
        var act = () => new SubtypeRegistry<Shape, ShapeKind>((ShapeKind.Square, typeof(Square)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*'box'*'square'*");
    }

    [Fact]
    public void KindOf_ShouldResolveTheConcreteType()
    {
        var registry = new SubtypeRegistry<Pet, PetKind>((PetKind.Cat, typeof(Cat)), (PetKind.Dog, typeof(Dog)));

        registry.KindOf(new Dog()).Should().Be(PetKind.Dog);
        registry.TypeOf(PetKind.Cat).Should().Be<Cat>();
        registry.Subtypes.Should().HaveCount(2);
    }
}
