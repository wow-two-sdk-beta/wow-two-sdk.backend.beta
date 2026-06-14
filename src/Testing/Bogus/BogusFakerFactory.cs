using Bogus;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Bogus;

/// <summary>
/// Conventional <see cref="global::Bogus.Faker{T}"/> wrappers with a deterministic seed for reproducible tests.
/// </summary>
public static class BogusFakerFactory
{
    /// <summary>Default seed for Bogus randomization. Override per fixture if needed.</summary>
    public const int DefaultSeed = 1337;

    /// <summary>Create a <see cref="Faker{T}"/> with the conventional default seed.</summary>
    public static Faker<T> For<T>() where T : class =>
        new Faker<T>().UseSeed(DefaultSeed);

    /// <summary>Create a <see cref="Faker{T}"/> with an explicit seed.</summary>
    public static Faker<T> For<T>(int seed) where T : class =>
        new Faker<T>().UseSeed(seed);

    /// <summary>Create a generic <see cref="Faker"/> for ad-hoc data with the default seed.</summary>
    public static Faker NewFaker(int seed = DefaultSeed) =>
        new Faker { Random = new global::Bogus.Randomizer(seed) };
}
