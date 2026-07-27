using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Http.HeaderPropagation;

/// <summary>Provides forwarding of inbound headers (correlation/request IDs) onto outbound <see cref="HttpClient"/> calls over <c>Microsoft.AspNetCore.HeaderPropagation</c>.</summary>
/// <remarks>Wire up three sides: register here, attach per client via <see cref="AddPropagatedHeaders"/>, and add capture middleware with <c>app.UseHeaderPropagation()</c>.</remarks>
public static class HeaderPropagationServiceCollectionExtensions
{
    /// <summary>Correlation-ID header propagated by default.</summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>Request-ID header propagated by default.</summary>
    public const string RequestIdHeader = "X-Request-Id";

    /// <summary>Registers propagation for the conventional headers (<see cref="CorrelationIdHeader"/>, <see cref="RequestIdHeader"/>) plus any <paramref name="additionalHeaders"/>. Pair with <c>app.UseHeaderPropagation()</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="additionalHeaders">Extra header names to propagate alongside the conventional ones.</param>
    public static IServiceCollection AddConventionalHeaderPropagation(
        this IServiceCollection services,
        params string[] additionalHeaders)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(additionalHeaders);

        return services.AddHeaderPropagation(o =>
        {
            o.Headers.Add(CorrelationIdHeader);
            o.Headers.Add(RequestIdHeader);
            foreach (var header in additionalHeaders)
            {
                o.Headers.Add(header);
            }
        });
    }

    /// <summary>Attaches headers registered via <see cref="AddConventionalHeaderPropagation"/> to every request this client sends.</summary>
    /// <param name="builder">The HTTP client builder to extend.</param>
    public static IHttpClientBuilder AddPropagatedHeaders(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddHeaderPropagation();
    }
}
