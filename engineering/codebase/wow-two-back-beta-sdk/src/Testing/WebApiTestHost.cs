using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace WoW.Two.Sdk.Backend.Beta.Testing;

/// <summary>
/// Test host wrapping <see cref="WebApplicationFactory{TEntryPoint}"/> with conventional defaults:
/// - Environment forced to <c>"Testing"</c>.
/// - <see cref="FakeTimeProvider"/> registered as the default <see cref="TimeProvider"/>.
/// - Hooks for replacing services and tweaking configuration before the host builds.
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
