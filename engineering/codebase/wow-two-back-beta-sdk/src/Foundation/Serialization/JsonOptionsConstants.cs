using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization.Converters;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

/// <summary>Holds conventional <see cref="JsonSerializerOptions"/> presets for the Wow Two backend SDK.</summary>
public static class JsonOptionsConstants
{
    /// <summary>Gets the wire options: camelCase, string enums, ISO durations, ignored nulls, NodaTime, and relaxed escaping.</summary>
    public static JsonSerializerOptions Default { get; } = Build();

    /// <summary>Gets the <see cref="Default"/> options with indented output, for human-readable dumps.</summary>
    public static JsonSerializerOptions Indented { get; } = Build(opt => opt.WriteIndented = true);

    private static JsonSerializerOptions Build(Action<JsonSerializerOptions>? customize = null)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new IsoDurationJsonConverter());
        customize?.Invoke(options);
        options.MakeReadOnly(populateMissingResolver: true); // populate the reflection resolver when none is set, so the preset works even where JsonSerializerIsReflectionEnabledByDefault=false
        return options;
    }
}
