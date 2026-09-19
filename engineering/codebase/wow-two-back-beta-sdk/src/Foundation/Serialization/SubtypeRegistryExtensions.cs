using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

/// <summary>Extends the subtype registries with their System.Text.Json binding.</summary>
public static class SubtypeRegistryExtensions
{
    private const string DefaultDiscriminatorPropertyName = "type";

    /// <summary>Builds a type-info modifier binding the registry's subtypes as a union on <typeparamref name="TBase"/>.</summary>
    /// <typeparam name="TBase">The polymorphic base type.</typeparam>
    /// <typeparam name="TKind">The enum discriminating the subtypes.</typeparam>
    /// <param name="registry">The registry to bind.</param>
    /// <param name="discriminatorPropertyName">The JSON property carrying the discriminator.</param>
    public static Action<JsonTypeInfo> ToJsonModifier<TBase, TKind>(
        this SubtypeRegistry<TBase, TKind> registry,
        string discriminatorPropertyName = DefaultDiscriminatorPropertyName)
        where TBase : class
        where TKind : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminatorPropertyName);

        return typeInfo =>
        {
            if (typeInfo.Type != typeof(TBase))
            {
                return;
            }

            var polymorphism = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = discriminatorPropertyName,
            };
            foreach (var (_, type, discriminator) in registry.Subtypes)
            {
                polymorphism.DerivedTypes.Add(new JsonDerivedType(type, discriminator));
            }

            typeInfo.PolymorphismOptions = polymorphism;
        };
    }
}
