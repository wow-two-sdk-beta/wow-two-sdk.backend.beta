using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json;

/// <summary>Value comparer for JSON-mapped CLR types — compares by serialized form, hashes by serialized string, and deep-clones via re-serialization.</summary>
/// <remarks>EF Core's snapshot/change-tracking machinery needs an explicit comparer for mutable reference types stored as JSON, otherwise updates are missed.</remarks>
/// <typeparam name="T">The CLR type being compared.</typeparam>
public sealed class JsonValueComparer<T> : ValueComparer<T>
{
    /// <summary>Initializes the comparer with default <see cref="JsonSerializerOptions"/>.</summary>
    public JsonValueComparer() : this(new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }

    /// <summary>Initializes the comparer with a custom <see cref="JsonSerializerOptions"/>.</summary>
    /// <param name="options">The serializer options used for comparison, hashing, and cloning.</param>
    public JsonValueComparer(JsonSerializerOptions options)
        : base(
            (left, right) => Serialize(left, options) == Serialize(right, options),
            value => Serialize(value, options).GetHashCode(StringComparison.Ordinal),
            value => Deserialize(value, options))
    {
    }

    private static string Serialize(T? value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(value, options);

    private static T Deserialize(T value, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, options), options)!;
}
