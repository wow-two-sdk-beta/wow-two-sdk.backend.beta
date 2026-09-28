using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Http.Errors;

/// <summary>Turns a dependency's failing response into an <see cref="AppError"/> that keeps its status and problem detail.</summary>
public static class HttpResponseErrorExtensions
{
    /// <summary>
    /// The error for a failing response, or null on success. Known statuses map as <see cref="HttpExceptionMappingRule"/>
    /// does; 404 is <c>NotFound</c>, 409 <c>Conflict</c>, other client errors <c>Unexpected</c>. Metadata carries
    /// <c>upstreamStatus</c>, the dependency's problem <c>detail</c> when it sent one, and <c>retryAfter</c>.
    /// </summary>
    /// <param name="response">The dependency's response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<AppError?> ToAppErrorAsync(this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.IsSuccessStatusCode)
            return null;

        var type = HttpExceptionMappingRule.ForStatus(response.StatusCode) ?? (int)response.StatusCode switch
        {
            404 => AppErrorType.NotFound,
            409 => AppErrorType.Conflict,
            _ => AppErrorType.Unexpected,
        };
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal) { ["upstreamStatus"] = (int)response.StatusCode };
        if (await ProblemDetailAsync(response, cancellationToken) is { } detail)
            metadata["upstreamDetail"] = detail;
        if (response.Headers.RetryAfter?.Delta is { } delay)
            metadata["retryAfter"] = ((int)Math.Ceiling(delay.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        return AppError.Of(type, ErrorMessageConstants.For(type), metadata);
    }

    /// <summary>Throws the <see cref="ToAppErrorAsync"/> error as an <see cref="AppException"/> when the response failed.</summary>
    /// <param name="response">The dependency's response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task EnsureSuccessAsync(this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (await response.ToAppErrorAsync(cancellationToken) is { } error)
            throw error.ToException();
    }

    private static async Task<string?> ProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType is not ("application/problem+json" or "application/json"))
            return null;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return problem.ValueKind == JsonValueKind.Object
                && (problem.TryGetProperty("detail", out var detail) || problem.TryGetProperty("title", out detail))
                && detail.ValueKind == JsonValueKind.String
                    ? detail.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
