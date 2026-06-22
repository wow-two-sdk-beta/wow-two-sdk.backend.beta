using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Web;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for E2E HTTP tests, aligned to the SDK's API serializer
/// (<c>WoW.Two.Sdk.Backend.Beta.Foundation.Serialization.JsonOptionsPresets.Default</c>).
/// </summary>
/// <remarks>
/// The Testing mono-lib is self-contained (no reference to the core lib), so the preset is mirrored here
/// rather than imported. Every deserialize- and request-body-relevant setting of <c>JsonOptionsPresets.Default</c>
/// is replicated — including the NodaTime converters via <c>ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)</c>
/// (the Testing project references <c>NodaTime</c> + <c>NodaTime.Serialization.SystemTextJson</c> for this), so tests
/// asserting NodaTime payloads work out of the box. <see cref="JsonStringEnumConverter"/> is included to match the
/// proven smart-qr helper; the string-enum converter reads both enum names and numeric values, so it is safe whether
/// the app under test emits string or numeric enums.
/// </remarks>
public static class TestJson
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

        // Match JsonOptionsPresets.Default exactly — register the NodaTime converters (Instant, LocalDate, …).
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        options.MakeReadOnly();
        return options;
    }
}

/// <summary>
/// Test-side mirror of the SDK success envelope (<c>ApiResponse&lt;T&gt;.Success</c>) carrying the payload under <c>data</c>.
/// </summary>
/// <remarks>
/// A mirror is used rather than the real <c>WoW.Two.Sdk.Backend.Beta.Web.Contracts.ApiResponse&lt;T&gt;</c> because:
/// (1) the Testing mono-lib does not reference the core lib that defines it; and (2) that type is an abstract record
/// with a private constructor and no polymorphic discriminator, so <c>System.Text.Json</c> cannot deserialize a
/// response into it directly. Only the <c>data</c> field is asserted — the wire contract — independent of the
/// production type.
/// </remarks>
/// <typeparam name="T">The wrapped payload type.</typeparam>
public sealed record ApiEnvelope<T>
{
    /// <summary>Gets the wrapped payload, deserialized from <c>.data</c>.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }
}
