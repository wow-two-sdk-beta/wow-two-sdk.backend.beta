using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Foundation.Time;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks;
using WoW.Two.Sdk.Backend.Beta.Observability.Logging;
using WoW.Two.Sdk.Backend.Beta.Observability.Metrics;
using WoW.Two.Sdk.Backend.Beta.Observability.Otlp;
using WoW.Two.Sdk.Backend.Beta.Observability.Tracing;
using WoW.Two.Sdk.Backend.Beta.Web.Compression;
using WoW.Two.Sdk.Backend.Beta.Web.Cors;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;
using WoW.Two.Sdk.Backend.Beta.Web.Hosting;
using WoW.Two.Sdk.Backend.Beta.Web.OpenApi;
using WoW.Two.Sdk.Backend.Beta.Web.OutputCache;
using WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;
using WoW.Two.Sdk.Backend.Beta.Web.RateLimit;
using WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders;

namespace WoW.Two.Sdk.Backend.Beta.Meta;

/// <summary>Provides the one-import P1 boot floor (logging, tracing, metrics, health, proxy-aware hosting, OpenAPI, problem details, rate limit, output cache, compression); auth, mediator, and data stay explicit.</summary>
public static class ApiDefaultsExtensions
{
    /// <summary>Registers the SDK service baseline; pair with <see cref="UseApiDefaults"/> after <c>Build()</c>.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configure">Optional flags / service name / validator assemblies / CORS origins.</param>
    public static WebApplicationBuilder AddApiDefaults(
        this WebApplicationBuilder builder,
        Action<ApiDefaultsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new ApiDefaultsOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);

        builder.Host.UseSerilogConventional();

        var serviceName = options.ServiceName ?? builder.Environment.ApplicationName;
        builder.Services
            .AddTimeProviders()
            .AddOpenTelemetryTracing(serviceName)
            .AddOpenTelemetryMetrics(serviceName);
        if (options.EnableOtlpExporters)
        {
            builder.Services.AddOtlpExporters();
        }

        builder.Services.AddHealthChecksBuilder();

        if (options.ValidatorAssemblies.Count > 0)
        {
            builder.Services.AddFluentValidatorsFromAssemblies([.. options.ValidatorAssemblies]);
        }

        builder.Services
            .AddProxyAwareHosting(hosting =>
            {
                foreach (var host in options.AllowedHosts)
                    hosting.AllowedHosts.Add(host);
            })
            .AddOpenApiDefaults()
            .AddTraceAwareProblemDetails()
            .AddAppExceptionHandling();

        if (options.EnableRateLimiting)
        {
            builder.Services.AddPerIpSlidingWindowRateLimit();
        }

        if (options.EnableOutputCache)
        {
            builder.Services.AddDefaultOutputCache();
        }

        if (options.EnableResponseCompression)
        {
            builder.Services.AddBrotliGzipCompression();
        }

        if (options.CorsOrigins.Count > 0)
        {
            builder.Services.AddDefaultCorsPolicy([.. options.CorsOrigins]);
        }

        return builder;
    }

    /// <summary>Adds the matching middleware pipeline (forwarded headers, secure headers, CORS, rate limiter, output cache, compression) and maps the OpenAPI and health endpoints; add auth and your endpoints after this call.</summary>
    /// <param name="app">The built application.</param>
    public static WebApplication UseApiDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetService<ApiDefaultsOptions>() ?? new ApiDefaultsOptions();

        // Outermost: route unhandled exceptions through the registered ProblemDetails handlers.
        app.UseExceptionHandler();

        app.UseProxyAwareHosting();

        if (options.EnableHttpsRedirection)
            app.UseHttpsRedirection();

        app.UseOwaspSecureHeaders();

        if (options.CorsOrigins.Count > 0)
        {
            app.UseCors();
        }

        if (options.EnableRateLimiting)
        {
            app.UseRateLimiter();
        }

        if (options.EnableOutputCache)
        {
            app.UseOutputCache();
        }

        if (options.EnableResponseCompression)
        {
            app.UseResponseCompression();
        }

        // Default-safe: expose the OpenAPI schema only in Development unless explicitly forced — avoids leaking API shape in Production (API9).
        if (options.ExposeOpenApi ?? app.Environment.IsDevelopment())
        {
            app.MapOpenApiEndpoint();
        }

        // Liveness must stay reachable by unauthenticated probes, even under a default-deny fallback policy.
        app.MapHealthChecks(options.HealthEndpointPath).AllowAnonymous();

        return app;
    }
}
