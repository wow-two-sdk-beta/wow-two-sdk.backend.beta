using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Web.Json;

/// <summary>Provides controller JSON presets from the SDK <see cref="JsonOptionsConstants.Default"/> wire options; <c>AddApiDefaults</c> registers no controllers, so a controller host opts in.</summary>
public static class JsonControllerBuilderExtensions
{
    /// <summary>Serializes enums as their camelCase string labels by adding a <see cref="JsonStringEnumConverter"/> (with <see cref="JsonNamingPolicy.CamelCase"/>) to the controller JSON options, leaving every other option untouched. This is the wire enum contract — camelCase strings, never PascalCase names or integer ordinals.</summary>
    /// <param name="builder">The MVC builder to configure.</param>
    public static IMvcBuilder AddJsonStringEnums(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
        return builder;
    }

    /// <summary>Registers controllers whose JSON options mirror the SDK <see cref="JsonOptionsConstants.Default"/> wire preset.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IMvcBuilder AddControllersWithSdkJson(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddControllers()
            .AddJsonOptions(options => ApplySdkJson(options.JsonSerializerOptions));
    }

    /// <summary>Copies the SDK <see cref="JsonOptionsConstants.Default"/> settings onto the target options.</summary>
    /// <param name="target">The live controller <see cref="JsonSerializerOptions"/> to mutate.</param>
    private static void ApplySdkJson(JsonSerializerOptions target)
    {
        var preset = JsonOptionsConstants.Default;

        target.PropertyNamingPolicy = preset.PropertyNamingPolicy;
        target.DictionaryKeyPolicy = preset.DictionaryKeyPolicy;
        target.DefaultIgnoreCondition = preset.DefaultIgnoreCondition;
        target.Encoder = preset.Encoder;
        target.ReadCommentHandling = preset.ReadCommentHandling;
        target.AllowTrailingCommas = preset.AllowTrailingCommas;
        target.NumberHandling = preset.NumberHandling;

        foreach (var converter in preset.Converters)
            target.Converters.Add(converter);

    }
}
