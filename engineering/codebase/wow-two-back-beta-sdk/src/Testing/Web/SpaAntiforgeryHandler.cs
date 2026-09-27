namespace WoW.Two.Sdk.Backend.Beta.Testing.Web;

/// <summary>Echoes a single-page app's antiforgery token on unsafe requests that carry cookies, as the browser client does.</summary>
/// <remarks>
///   - before each such request, a GET to the token path issues a token bound to the same cookies
///   - the issued cookies join the request's <c>Cookie</c> header, and the token goes out in the echo header
///   - safe methods, requests without cookies and requests that already carry the header pass unchanged
/// </remarks>
/// <example>
/// <code>
/// using var client = host.CreateDefaultClient(new SpaAntiforgeryHandler("/health"));
/// client.DefaultRequestHeaders.Add("Cookie", $"session={cookie}");
/// await client.PostAsync("/api/codes", body);   // passes the SDK's SPA antiforgery middleware
/// </code>
/// </example>
public sealed class SpaAntiforgeryHandler : DelegatingHandler
{
    private const string CookieHeader = "Cookie";
    private readonly string _tokenPath;
    private readonly string _cookieName;
    private readonly string _headerName;

    /// <summary>Creates the handler for a token path and the SDK's cookie and header names.</summary>
    /// <param name="tokenPath">A path whose GET passes through the antiforgery middleware, such as <c>/health</c>.</param>
    /// <param name="cookieName">The readable token cookie. Defaults to <c>XSRF-TOKEN</c>.</param>
    /// <param name="headerName">The echo header. Defaults to <c>X-XSRF-TOKEN</c>.</param>
    public SpaAntiforgeryHandler(string tokenPath = "/", string cookieName = "XSRF-TOKEN", string headerName = "X-XSRF-TOKEN")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        _tokenPath = tokenPath;
        _cookieName = cookieName;
        _headerName = headerName;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsSafe(request.Method) || request.Headers.Contains(_headerName)
            || !request.Headers.TryGetValues(CookieHeader, out IEnumerable<string>? header))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        Dictionary<string, string> cookies = ParseCookies(header);
        using (var tokenRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri!, _tokenPath)))
        {
            tokenRequest.Headers.TryAddWithoutValidation(CookieHeader, FormatCookies(cookies));
            using HttpResponseMessage tokenResponse = await base.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
            foreach ((string name, string value) in IssuedCookies(tokenResponse))
            {
                cookies[name] = value;
            }
        }

        if (cookies.TryGetValue(_cookieName, out string? token))
        {
            request.Headers.Remove(CookieHeader);
            request.Headers.TryAddWithoutValidation(CookieHeader, FormatCookies(cookies));
            request.Headers.TryAddWithoutValidation(_headerName, Uri.UnescapeDataString(token));
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSafe(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options || method == HttpMethod.Trace;

    private static Dictionary<string, string> ParseCookies(IEnumerable<string> header)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string pair in header.SelectMany(static value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            int separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                cookies[pair[..separator]] = pair[(separator + 1)..];
            }
        }

        return cookies;
    }

    private static IEnumerable<(string Name, string Value)> IssuedCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
        {
            yield break;
        }

        foreach (string value in values)
        {
            string pair = value.Split(';', 2)[0].Trim();
            int separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                yield return (pair[..separator], pair[(separator + 1)..]);
            }
        }
    }

    private static string FormatCookies(Dictionary<string, string> cookies) =>
        string.Join("; ", cookies.Select(static cookie => $"{cookie.Key}={cookie.Value}"));
}
