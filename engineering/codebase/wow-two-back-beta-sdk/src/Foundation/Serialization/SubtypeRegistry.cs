using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

/// <summary>Binds each <typeparamref name="TKind"/> member to the <typeparamref name="TBase"/> subtype it identifies, so a union declared outside its types is closed and complete.</summary>
/// <typeparam name="TBase">The polymorphic base type.</typeparam>
/// <typeparam name="TKind">The enum discriminating the subtypes.</typeparam>
/// <remarks>
///   - declare once as a static member of the base; bind to JSON with <see cref="SubtypeRegistryExtensions.ToJsonModifier{TBase, TKind}"/>
///   - a base that also carries <see cref="JsonDerivedTypeAttribute"/> is checked against it, so the two declarations cannot drift
///   - the discriminator is the member's camelCase name, so a <see cref="JsonStringEnumMemberNameAttribute"/> override is honoured
/// </remarks>
public sealed class SubtypeRegistry<TBase, TKind>
    where TBase : class
    where TKind : struct, Enum
{
    private static readonly JsonSerializerOptions DiscriminatorOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Dictionary<TKind, Type> _typesByKind;
    private readonly Dictionary<Type, TKind> _kindsByType;

    /// <summary>Creates the registry, requiring one subtype per <typeparamref name="TKind"/> member.</summary>
    /// <param name="subtypes">Each discriminator member paired with the concrete type it identifies.</param>
    /// <exception cref="ArgumentException">A subtype does not derive from <typeparamref name="TBase"/> or is abstract, a kind or type repeats, or a <typeparamref name="TKind"/> member has no subtype.</exception>
    /// <exception cref="InvalidOperationException">The base declares <see cref="JsonDerivedTypeAttribute"/> bindings that disagree with these.</exception>
    public SubtypeRegistry(params (TKind Kind, Type Type)[] subtypes)
    {
        ArgumentNullException.ThrowIfNull(subtypes);

        foreach (var (_, type) in subtypes)
        {
            if (!typeof(TBase).IsAssignableFrom(type))
            {
                throw new ArgumentException($"'{type.Name}' does not derive from '{typeof(TBase).Name}'.", nameof(subtypes));
            }

            if (type.IsAbstract)
            {
                throw new ArgumentException($"'{type.Name}' is abstract and cannot be a subtype.", nameof(subtypes));
            }
        }

        _typesByKind = [];
        _kindsByType = [];

        foreach (var (kind, type) in subtypes)
        {
            if (!_typesByKind.TryAdd(kind, type))
            {
                throw new ArgumentException($"'{kind}' is mapped more than once.", nameof(subtypes));
            }

            if (!_kindsByType.TryAdd(type, kind))
            {
                throw new ArgumentException($"'{type.Name}' is mapped more than once.", nameof(subtypes));
            }
        }

        var missing = Enum.GetValues<TKind>().Where(kind => !_typesByKind.ContainsKey(kind)).ToArray();
        if (missing.Length > 0)
        {
            throw new ArgumentException($"'{typeof(TKind).Name}' members without a subtype: {string.Join(", ", missing)}.", nameof(subtypes));
        }

        Subtypes = subtypes
            .Select(subtype => (subtype.Kind, subtype.Type, Discriminator: ToDiscriminator(subtype.Kind)))
            .ToArray();

        VerifyAgainstAttributes();
    }

    /// <summary>Gets every subtype, paired with its discriminator member and the token it serializes to.</summary>
    public IReadOnlyList<(TKind Kind, Type Type, string Discriminator)> Subtypes { get; }

    /// <summary>Gets the concrete type a discriminator member identifies.</summary>
    /// <param name="kind">The discriminator member.</param>
    /// <exception cref="InvalidOperationException">No subtype is bound to the member.</exception>
    public Type TypeOf(TKind kind)
    {
        return _typesByKind.TryGetValue(kind, out var type)
            ? type
            : throw new InvalidOperationException($"No subtype is bound to '{kind}'.");
    }

    /// <summary>Gets the discriminator member of an instance's concrete type.</summary>
    /// <param name="instance">The instance to classify.</param>
    /// <exception cref="InvalidOperationException">The instance's type is not a registered subtype.</exception>
    public TKind KindOf(TBase instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return _kindsByType.TryGetValue(instance.GetType(), out var kind)
            ? kind
            : throw new InvalidOperationException($"'{instance.GetType().Name}' is not a registered subtype of '{typeof(TBase).Name}'.");
    }

    private static string ToDiscriminator(TKind kind)
    {
        return JsonSerializer.Serialize(kind, DiscriminatorOptions).Trim('"');
    }

    private void VerifyAgainstAttributes()
    {
        var declared = typeof(TBase).GetCustomAttributes<JsonDerivedTypeAttribute>().ToArray();
        if (declared.Length == 0)
        {
            return;
        }

        var mismatches = new List<string>();
        foreach (var (_, type, discriminator) in Subtypes)
        {
            var attribute = declared.FirstOrDefault(candidate => candidate.DerivedType == type);
            if (attribute is null)
            {
                mismatches.Add($"'{type.Name}' has no [JsonDerivedType] on '{typeof(TBase).Name}'");
            }
            else if (!Equals(attribute.TypeDiscriminator, discriminator))
            {
                mismatches.Add($"'{type.Name}' is '{attribute.TypeDiscriminator}' in the attribute and '{discriminator}' here");
            }
        }

        foreach (var attribute in declared.Where(candidate => !_kindsByType.ContainsKey(candidate.DerivedType)))
        {
            mismatches.Add($"'{attribute.DerivedType.Name}' is declared on '{typeof(TBase).Name}' but has no '{typeof(TKind).Name}' member");
        }

        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException($"The [JsonDerivedType] declarations on '{typeof(TBase).Name}' disagree with its registry: {string.Join("; ", mismatches)}.");
        }
    }
}
