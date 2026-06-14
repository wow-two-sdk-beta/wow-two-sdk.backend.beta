using System.Reflection;

namespace WoW.Two.Sdk.Backend.Beta.Meta;

/// <summary>
/// Tunable inputs for <c>AddApiDefaults</c> / <c>UseApiDefaults</c>. Everything defaults to on —
/// flip a flag off rather than re-composing the per-area extensions by hand.
/// </summary>
public sealed class ApiDefaultsOptions
{
    /// <summary>OpenTelemetry service name. Defaults to the host's application name.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Assemblies scanned for FluentValidation validators. Empty = skip validator registration.</summary>
    public IList<Assembly> ValidatorAssemblies { get; } = [];

    /// <summary>Allowed CORS origins for the default policy. Empty = CORS not registered.</summary>
    public IList<string> CorsOrigins { get; } = [];

    /// <summary>Export traces + metrics over OTLP (endpoint from config/env). Default on.</summary>
    public bool EnableOtlpExporters { get; set; } = true;

    /// <summary>Per-IP sliding-window rate limiting. Default on.</summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>Output caching middleware with the 60s default policy. Default on.</summary>
    public bool EnableOutputCache { get; set; } = true;

    /// <summary>Brotli + Gzip response compression. Default on.</summary>
    public bool EnableResponseCompression { get; set; } = true;

    /// <summary>Map the OpenAPI document endpoint. Default on.</summary>
    public bool ExposeOpenApi { get; set; } = true;

    /// <summary>Liveness endpoint path. Default <c>/health</c>.</summary>
    public string HealthEndpointPath { get; set; } = "/health";
}
