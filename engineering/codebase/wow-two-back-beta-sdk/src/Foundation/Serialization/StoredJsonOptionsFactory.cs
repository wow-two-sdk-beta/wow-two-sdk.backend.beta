using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

/// <summary>Creates a pinned stored-JSON options instance — the shared preset plus the given type-info modifiers, one instance per document root a product declares its unions for outside the types.</summary>
/// <remarks>
///   - the preset writes nulls, so a <c>required</c> nullable member round-trips; the wire preset omits them
///   - metadata may arrive in any order, because jsonb reorders object keys
///   - build the instance once in the owning composition scope or register it as a keyed profile; System.Text.Json caches type metadata per instance
/// </remarks>
public static class StoredJsonOptionsFactory
{
    /// <summary>Creates the options, binding each modifier's union onto the resolver.</summary>
    /// <param name="modifiers">Type-info modifiers — a <see cref="SubtypeRegistryExtensions.ToJsonModifier{TBase, TKind}"/> per polymorphic base the document contains.</param>
    public static JsonSerializerOptions Create(params Action<JsonTypeInfo>[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        var resolver = new DefaultJsonTypeInfoResolver();
        foreach (var modifier in modifiers)
        {
            resolver.Modifiers.Add(modifier);
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            AllowOutOfOrderMetadataProperties = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            TypeInfoResolver = resolver,
        };

        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        options.MakeReadOnly();
        return options;
    }
}
