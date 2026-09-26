using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using NodaTime;

namespace WoW.Two.Sdk.Backend.Beta.Testing;

/// <summary>
/// Test host wrapping <see cref="WebApplicationFactory{TEntryPoint}"/> with conventional defaults:
/// - Environment defaults to <c>Development</c> so the host validates dependency lifetimes.
/// - <see cref="FakeTimeProvider"/> registered as the default <see cref="TimeProvider"/>.
/// - <see cref="IClock"/> adapted to that same fake clock.
/// - Hooks for replacing services and adding host-local configuration before the host builds.
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
    /// Adds host-local configuration before the host builds. Bind settings lazily through DI or options.
    /// </summary>
    public Action<IConfigurationBuilder>? ConfigureConfigurationHook { get; init; }

    /// <summary>
    /// Adds an additional `IHostBuilder` configuration step. Useful for `UseEnvironment("Production")` overrides.
    /// </summary>
    public Action<IHostBuilder>? ConfigureHostHook { get; init; }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
            ConfigureConfigurationHook?.Invoke(configuration));

        builder.ConfigureServices(services =>
        {
            // Replace both clock surfaces so one test clock controls every time read.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new TestClock(Clock));

            ConfigureServicesHook?.Invoke(services);
        });
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ConfigureHostHook?.Invoke(builder);
        return base.CreateHost(builder);
    }

    private sealed class TestClock(TimeProvider timeProvider) : IClock
    {
        public Instant GetCurrentInstant() => Instant.FromDateTimeOffset(timeProvider.GetUtcNow());
    }
}
