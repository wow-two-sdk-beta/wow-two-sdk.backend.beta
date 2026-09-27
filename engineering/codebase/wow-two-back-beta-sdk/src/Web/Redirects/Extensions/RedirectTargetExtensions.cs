using System.Net;
using FluentValidation;
using WoW.Two.Sdk.Backend.Beta.Http.Safety.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Web.Redirects.Extensions;

/// <summary>Extends FluentValidation rules with advisory checks on where a redirect target leads.</summary>
public static class RedirectTargetExtensions
{
    private static readonly string[] PrivateSuffixes =
        [".local", ".localhost", ".internal", ".intranet", ".lan", ".home", ".corp", ".private", ".home.arpa"];

    private static readonly string[] ReservedSuffixes = [".test", ".example", ".invalid"];

    private static readonly OutboundAddressValidator Addresses = new();

    /// <summary>Adds warnings and suggestions about a redirect target; none of them blocks the value.</summary>
    /// <param name="rule">The rule for a URL member.</param>
    /// <returns>The rule, for further chaining.</returns>
    /// <remarks>
    ///   - a target behind a VPN, login or intranet can be legitimate, so every finding is advisory
    ///   - a value that is not an absolute http(s) URL passes; keep that structural rule as an error
    ///   - declare it in its own <c>RuleFor</c>: a stopping cascade would hide later advisories
    ///   - warnings: <c>RedirectTargetPrivateNetwork</c>, <c>RedirectTargetReservedName</c>,
    ///     <c>RedirectTargetInsecure</c>, <c>RedirectTargetCredentials</c>
    ///   - suggestions: <c>RedirectTargetIpAddress</c>, <c>RedirectTargetPort</c>, <c>RedirectTargetInternationalHost</c>
    /// </remarks>
    public static IRuleBuilderOptions<T, string> AdviseOnRedirectTarget<T>(this IRuleBuilder<T, string> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule
            .Must(url => Passes(url, IsPublicHost))
            .WithSeverity(Severity.Warning).WithErrorCode("RedirectTargetPrivateNetwork")
            .WithMessage("This address opens only inside a private network; scans elsewhere cannot reach it.")
            .Must(url => Passes(url, uri => !HasSuffix(uri, ReservedSuffixes)))
            .WithSeverity(Severity.Warning).WithErrorCode("RedirectTargetReservedName")
            .WithMessage("This domain is reserved for testing and does not resolve on the internet.")
            .Must(url => Passes(url, uri => uri.Scheme == Uri.UriSchemeHttps))
            .WithSeverity(Severity.Warning).WithErrorCode("RedirectTargetInsecure")
            .WithMessage("This address uses http, so browsers may warn visitors; prefer https.")
            .Must(url => Passes(url, uri => uri.UserInfo.Length == 0))
            .WithSeverity(Severity.Warning).WithErrorCode("RedirectTargetCredentials")
            .WithMessage("This address embeds credentials that anyone who scans the code can read.")
            .Must(url => Passes(url, uri => !IsPublicIpLiteral(uri)))
            .WithSeverity(Severity.Info).WithErrorCode("RedirectTargetIpAddress")
            .WithMessage("This address uses an IP address; a domain name keeps working when the server moves.")
            .Must(url => Passes(url, uri => uri.IsDefaultPort))
            .WithSeverity(Severity.Info).WithErrorCode("RedirectTargetPort")
            .WithMessage("This address uses a non-standard port, which some networks block.")
            .Must(url => Passes(url, uri => !uri.IdnHost.Split('.').Any(IsPunycodeLabel)))
            .WithSeverity(Severity.Info).WithErrorCode("RedirectTargetInternationalHost")
            .WithMessage("This address uses an internationalized domain; confirm it is the intended site.");
    }

    private static bool Passes(string? url, Func<Uri, bool> check) =>
        !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        || uri.Scheme is not ("http" or "https")
        || check(uri);

    private static bool IsPublicHost(Uri uri)
    {
        if (TryGetAddress(uri, out IPAddress? address))
        {
            return Addresses.IsAllowed(address);
        }

        string host = uri.IdnHost.TrimEnd('.');
        return host.Contains('.', StringComparison.Ordinal)
            && !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            && !HasSuffix(uri, PrivateSuffixes);
    }

    private static bool IsPublicIpLiteral(Uri uri) =>
        TryGetAddress(uri, out IPAddress? address) && Addresses.IsAllowed(address);

    private static bool TryGetAddress(Uri uri, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IPAddress? address)
    {
        address = null;
        return uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            && IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out address);
    }

    private static bool HasSuffix(Uri uri, string[] suffixes)
    {
        string host = uri.IdnHost.TrimEnd('.');
        return suffixes.Any(suffix =>
            host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, suffix[1..], StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPunycodeLabel(string label) =>
        label.StartsWith("xn--", StringComparison.OrdinalIgnoreCase);
}
