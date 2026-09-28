using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace WoW.Two.Sdk.Backend.Beta.Web.ConditionalRequests;

/// <summary>Entity tags from row versions: tag a response and check <c>If-Match</c> before a write.</summary>
public static class EntityTagExtensions
{
    /// <summary>Sets a strong <c>ETag</c> from a row version (xmin, rowversion or concurrency stamp); the middleware then honours it.</summary>
    /// <param name="response">The response.</param>
    /// <param name="version">The version token.</param>
    public static void SetEntityTag(this HttpResponse response, object version)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = TagOf(version).ToString();
    }

    /// <summary>
    /// Whether an <c>If-Match</c> header is present and names neither <paramref name="currentVersion"/> nor <c>*</c> — the
    /// write must then answer <c>412 Precondition Failed</c>. A request without the header is not refused.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="currentVersion">The stored row's current version token.</param>
    public static bool FailsIfMatch(this HttpRequest request, object currentVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        var candidates = request.GetTypedHeaders().IfMatch;
        if (candidates.Count == 0)
            return false;

        var current = TagOf(currentVersion);
        return !candidates.Any(candidate => candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(current, useStrongComparison: true));
    }

    private static EntityTagHeaderValue TagOf(object version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var text = Convert.ToString(version, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return new EntityTagHeaderValue($"\"{text.Replace("\"", string.Empty, StringComparison.Ordinal)}\"");
    }
}
