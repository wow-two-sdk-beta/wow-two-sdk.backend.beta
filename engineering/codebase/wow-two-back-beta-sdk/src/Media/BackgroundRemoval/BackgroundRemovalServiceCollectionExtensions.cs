using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Media.BackgroundRemoval;

/// <summary>Registers the optional private worker adapter without model or asset persistence.</summary>
public static class BackgroundRemovalServiceCollectionExtensions
{
    /// <summary>Registers a non-redirecting worker client with host-supplied configuration.</summary>
    public static IServiceCollection AddBackgroundRemoval(this IServiceCollection services, Action<BackgroundRemovalOptions>? configure = null)
    {
        services.AddOptions<BackgroundRemovalOptions>().Configure(o => configure?.Invoke(o));
        services.AddHttpClient<IBackgroundRemovalService, HttpBackgroundRemovalService>(c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        return services;
    }
}
