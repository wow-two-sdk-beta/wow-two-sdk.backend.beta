namespace WoW.Two.Sdk.Backend.Beta.Testing.Auth;

/// <summary>
/// Helpers for round-tripping cookies through a <c>WebApplicationFactory&lt;T&gt;</c> test host whose endpoints
/// set <c>Secure</c> cookies that won't auto-attach over the host's <c>http://</c> transport.
/// </summary>
/// <remarks>
/// Pattern: call an endpoint that signs the client in (guest provisioning, OAuth callback, etc.), lift the
/// cookie out of its <c>Set-Cookie</c> response header with <see cref="ExtractCookie"/>, then re-attach it to
/// a fresh client with <c>AttachCookie</c> so subsequent requests are authenticated.
/// </remarks>
public static class CookieExtraction
{
    /// <summary>
    /// Lifts a cookie value out of a response's <c>Set-Cookie</c> headers by name, or <c>null</c> when absent.
    /// </summary>
    /// <param name="response">The response whose <c>Set-Cookie</c> headers are scanned.</param>
    /// <param name="name">The cookie name to look up (the part before <c>=</c>).</param>
    /// <returns>The cookie's value, or <c>null</c> if no <c>Set-Cookie</c> header sets that name.</returns>
    public static string? ExtractCookie(HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        var prefix = $"{name}=";
        foreach (var cookie in cookies)
        {
            // e.g. "app-auth=<value>; path=/; secure; httponly; samesite=lax"
            var head = cookie.Split(';', 2)[0].Trim();
            if (head.StartsWith(prefix, StringComparison.Ordinal))
                return head[prefix.Length..];
        }

        return null;
    }

    /// <summary>
    /// Lifts a cookie out of <paramref name="response"/> by name and re-attaches it to <paramref name="client"/>
    /// as a raw <c>Cookie</c> header, so the client authenticates on subsequent requests.
    /// </summary>
    /// <param name="client">The client to attach the cookie to (typically a fresh one from the test host).</param>
    /// <param name="response">The response carrying the <c>Set-Cookie</c> header to lift the cookie from.</param>
    /// <param name="name">The cookie name to carry over.</param>
    /// <returns>The same <paramref name="client"/>, for chaining.</returns>
    /// <exception cref="InvalidOperationException">No <c>Set-Cookie</c> header set a cookie named <paramref name="name"/>.</exception>
    public static HttpClient AttachCookie(this HttpClient client, HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(client);

        var value = ExtractCookie(response, name)
            ?? throw new InvalidOperationException($"Response did not set a cookie named '{name}'.");

        return client.AttachCookie(name, value);
    }

    /// <summary>
    /// Appends a raw <c>name=value</c> cookie to the client's default <c>Cookie</c> request header.
    /// Existing cookies on the header are preserved.
    /// </summary>
    /// <param name="client">The client to attach the cookie to.</param>
    /// <param name="name">The cookie name.</param>
    /// <param name="value">The cookie value.</param>
    /// <returns>The same <paramref name="client"/>, for chaining.</returns>
    public static HttpClient AttachCookie(this HttpClient client, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(name);

        client.DefaultRequestHeaders.Add("Cookie", $"{name}={value}");
        return client;
    }
}
