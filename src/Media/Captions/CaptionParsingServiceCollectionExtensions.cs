using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Registration helpers for the Media.Captions package.</summary>
public static class CaptionParsingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IVttCaptionParser"/> (implemented by <see cref="VttCaptionParser"/>) as a singleton.
    /// The parser is stateless and thread-safe; the call is idempotent.
    /// </summary>
    /// <param name="services">The service collection to add the parser to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddVttCaptionParser(this IServiceCollection services)
    {
        services.TryAddSingleton<IVttCaptionParser, VttCaptionParser>();
        return services;
    }
}
