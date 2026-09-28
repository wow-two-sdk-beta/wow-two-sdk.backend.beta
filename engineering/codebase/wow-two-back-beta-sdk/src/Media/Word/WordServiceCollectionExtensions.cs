using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Media.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Word processing registration.</summary>
public static class WordServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IWordService"/> over the Open XML SDK and maps <see cref="WordRejectedException"/> to a 400.
    /// Options come from <paramref name="configure"/>, then the host section <c>Media:Word</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Limits and typography.</param>
    public static IServiceCollection AddWordProcessing(this IServiceCollection services, Action<WordOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            WordOptions.SectionName,
            configure,
            builder => builder
                .Validate(o => o.MaxInputBytes > 0 && o.MaxCharactersPerPart > 0, "WordOptions limits must be positive.")
                .Validate(o => o.FontSizePoints is > 0 and <= 400, "WordOptions.FontSizePoints must be between 0 and 400."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWordService, OpenXmlWordService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionMappingRule, MediaExceptionMappingRule>());
        return services;
    }
}
