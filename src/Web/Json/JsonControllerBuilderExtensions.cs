using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Web.Json;

/// <summary>Controller JSON presets — string enums and the SDK <see cref="JsonOptionsPresets.Default"/> options; <c>AddApiDefaults</c> registers no controllers, so a controller host opts in.</summary>
public static class JsonControllerBuilderExtensions
{
    /// <summary>Serializes enums as their string labels by adding a <see cref="JsonStringEnumConverter"/> to the controller JSON options, leaving every other option untouched.</summary>
    /// <param name="builder">The MVC builder to configure.</param>
    public static IMvcBuilder AddJsonStringEnums(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return builder;
    }

    /// <summary>Registers controllers whose JSON options mirror the SDK <see cref="JsonOptionsPresets.Default"/> preset (camelCase, null-ignoring, NodaTime, relaxed escaping) plus string-enum serialization.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IMvcBuilder AddControllersWithSdkJson(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddControllers()
            .AddJsonOptions(options => ApplySdkJson(options.JsonSerializerOptions));
    }

    /// <summary>Copies the SDK <see cref="JsonOptionsPresets.Default"/> settings onto the target options and adds the string-enum converter.</summary>
    /// <param name="target">The live controller <see cref="JsonSerializerOptions"/> to mutate.</param>
    private static void ApplySdkJson(JsonSerializerOptions target)
    {
        var preset = JsonOptionsPresets.Default;

        target.PropertyNamingPolicy = preset.PropertyNamingPolicy;
        target.DictionaryKeyPolicy = preset.DictionaryKeyPolicy;
        target.DefaultIgnoreCondition = preset.DefaultIgnoreCondition;
        target.Encoder = preset.Encoder;
        target.ReadCommentHandling = preset.ReadCommentHandling;
        target.AllowTrailingCommas = preset.AllowTrailingCommas;
        target.NumberHandling = preset.NumberHandling;

        foreach (var converter in preset.Converters)
            target.Converters.Add(converter);

        target.Converters.Add(new JsonStringEnumConverter());
    }
}
