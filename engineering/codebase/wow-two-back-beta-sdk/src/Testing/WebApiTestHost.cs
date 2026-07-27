using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace WoW.Two.Sdk.Backend.Beta.Testing;

/// <summary>
/// Test host wrapping <see cref="WebApplicationFactory{TEntryPoint}"/> with conventional defaults:
/// <list type="bullet">
///   <item>Environment forced to <c>"Testing"</c>.</item>
///   <item><see cref="FakeTimeProvider"/> registered as the default <see cref="TimeProvider"/>.</item>
///   <item>Hooks for replacing services and tweaking configuration before the host builds.</item>
/// </list>
/// </summary>
/// <typeparam name="TEntryPoint">The application entry-point type (typically <c>Program</c>).</typeparam>
public class WebApiTestHost<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    /// <summary>
    /// The fake clock injected as the default <see cref="TimeProvider"/>. Mutate from tests to advance time.
    /// </summary>
    public FakeTimeProvider Clock { get; } = new();

    /// <summary>
    /// Adds a service-replacement hook. Called once when the host builds.
    /// </summary>
    public Action<IServiceCollection>? ConfigureServicesHook { get; init; }

    /// <summary>
    /// Adds an additional `IHostBuilder` configuration step. Useful for `UseEnvironment("Production")` overrides.
    /// </summary>
    public Action<IHostBuilder>? ConfigureHostHook { get; init; }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production); // typically Production-shape; override via ConfigureHostHook

        builder.ConfigureServices(services =>
        {
            // Default: replace TimeProvider with FakeTimeProvider so tests control time.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            ConfigureServicesHook?.Invoke(services);
        });
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ConfigureHostHook?.Invoke(builder);
        return base.CreateHost(builder);
    }
}

internal static class ServiceCollectionInternalExtensions
{
    public static IServiceCollection RemoveAll<TService>(this IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
                services.RemoveAt(i);
        }

        return services;
    }
}
