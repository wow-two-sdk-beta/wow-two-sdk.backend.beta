using System.Reflection;

namespace WoW.Two.Sdk.Backend.Beta.Meta;

/// <summary>Configuration for <c>AddApiDefaults</c> / <c>UseApiDefaults</c>; every toggle defaults on.</summary>
public sealed class ApiDefaultsOptions
{
    /// <summary>OpenTelemetry service name. Defaults to the host's application name.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Assemblies scanned for FluentValidation validators. Empty = skip validator registration.</summary>
    public IList<Assembly> ValidatorAssemblies { get; } = [];

    /// <summary>Allowed CORS origins for the default policy. Empty = CORS not registered.</summary>
    public IList<string> CorsOrigins { get; } = [];

    /// <summary>Export traces and metrics over OTLP (endpoint from config/env). Default on.</summary>
    public bool EnableOtlpExporters { get; set; } = true;

    /// <summary>Per-IP sliding-window rate limiting. Default on.</summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>Output caching middleware with the 60s default policy. Default on.</summary>
    public bool EnableOutputCache { get; set; } = true;

    /// <summary>Brotli and Gzip response compression. Default on.</summary>
    public bool EnableResponseCompression { get; set; } = true;

    /// <summary>Map the OpenAPI document endpoint. Default on.</summary>
    public bool ExposeOpenApi { get; set; } = true;

    /// <summary>Liveness endpoint path. Default <c>/health</c>.</summary>
    public string HealthEndpointPath { get; set; } = "/health";
}
