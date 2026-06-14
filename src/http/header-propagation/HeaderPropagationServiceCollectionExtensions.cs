using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.http.header_propagation;

/// <summary>
/// Forwards selected inbound request headers onto outbound <see cref="HttpClient"/> calls
/// (correlation / request IDs by default). Wraps <c>Microsoft.AspNetCore.HeaderPropagation</c>.
/// Wire-up is three-sided: register here, attach per client via <see cref="AddPropagatedHeaders"/>,
/// and add the capture middleware with <c>app.UseHeaderPropagation()</c>.
/// </summary>
public static class HeaderPropagationServiceCollectionExtensions
{
    /// <summary>Correlation-ID header propagated by default.</summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>Request-ID header propagated by default.</summary>
    public const string RequestIdHeader = "X-Request-Id";

    /// <summary>
    /// Registers header propagation for the conventional headers (<see cref="CorrelationIdHeader"/>,
    /// <see cref="RequestIdHeader"/>) plus any <paramref name="additionalHeaders"/>.
    /// Pair with <c>app.UseHeaderPropagation()</c> to capture inbound values.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="additionalHeaders">Extra header names to propagate beyond the conventional pair.</param>
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

    /// <summary>
    /// Attaches the propagated headers registered via <see cref="AddConventionalHeaderPropagation"/>
    /// to every request this client sends.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    public static IHttpClientBuilder AddPropagatedHeaders(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddHeaderPropagation();
    }
}
