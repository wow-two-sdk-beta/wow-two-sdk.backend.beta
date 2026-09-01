using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Web;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for E2E HTTP tests, aligned to the SDK's API serializer
/// (<c>WoW.Two.Sdk.Backend.Beta.Foundation.Serialization.JsonOptionsConstants.Default</c>).
/// </summary>
/// <remarks>
///   - the NodaTime converters are registered against Tzdb, so a NodaTime payload deserializes unaided
///   - enums read from both names and numeric values, whatever the app under test emits
/// </remarks>
public static class TestJsonConstants
{
    /// <summary>
    /// Options matching the SDK API wire contract: <see cref="JsonSerializerDefaults.Web"/> base, camelCase property
    /// and dictionary keys, null-ignoring writes, lenient number/comment/trailing-comma reads, relaxed JS escaping,
    /// string enums, and NodaTime types.
    /// </summary>
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new JsonStringEnumConverter() },
        };

        // Match JsonOptionsConstants.Default exactly — register the NodaTime converters (Instant, LocalDate, …).
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        // populateMissingResolver: true — on .NET 10 the parameterless MakeReadOnly() throws with no resolver set.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
