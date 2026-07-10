using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

/// <summary>Provides conventional <see cref="JsonSerializerOptions"/> presets for the Wow Two backend SDK.</summary>
public static class JsonOptionsPresets
{
    /// <summary>Gets the default options: camelCase, ignore null on writes, NodaTime support, relaxed escaping.</summary>
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
        customize?.Invoke(options);
        options.MakeReadOnly(populateMissingResolver: true); // populate the reflection resolver when none is set, so the preset works even where JsonSerializerIsReflectionEnabledByDefault=false
        return options;
    }
}
