using System.Net.Http;

namespace WoW.Two.Sdk.Backend.Beta.Http.Resilience.Extensions;

/// <summary>Extends HTTP replay safety with request eligibility checks for retry and hedging.</summary>
public static class HttpReplaySafetyExtensions
{
    /// <summary>Returns whether <paramref name="request"/> may be replayed under the SDK contract.</summary>
    /// <remarks>
    /// GET, HEAD, OPTIONS and TRACE are replay-safe by default. Other methods require the
    /// client-level selector. <see cref="StreamContent"/> is never replayed because its
    /// underlying stream may be forward-only or already consumed.
    /// </remarks>
    public static bool IsReplaySafe(
        this HttpRequestMessage request,
        Func<HttpRequestMessage, bool>? unsafeRequestReplaySelector = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is StreamContent)
            return false;

        return request.Method == HttpMethod.Get
               || request.Method == HttpMethod.Head
               || request.Method == HttpMethod.Options
               || request.Method == HttpMethod.Trace
               || unsafeRequestReplaySelector?.Invoke(request) == true;
    }
}
