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

    /// <summary>Map the OpenAPI document endpoint. <c>null</c> (default) = only in Development; set <c>true</c>/<c>false</c> to force. Exposing the schema in Production leaks API shape (OWASP API9).</summary>
    public bool? ExposeOpenApi { get; set; }

    /// <summary>Redirect HTTP requests to HTTPS (honors <c>X-Forwarded-Proto</c> behind the proxy-aware forwarded-headers middleware). Default on.</summary>
    public bool EnableHttpsRedirection { get; set; } = true;

    /// <summary>Host-header allowlist (host filtering). Empty (default) = allow any host; set to lock the app to known hostnames.</summary>
    public IList<string> AllowedHosts { get; } = [];

    /// <summary>Liveness endpoint path. Default <c>/health</c>.</summary>
    public string HealthEndpointPath { get; set; } = "/health";
}
