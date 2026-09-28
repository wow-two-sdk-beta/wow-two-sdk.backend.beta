using Microsoft.Extensions.Configuration;

namespace WoW.Two.Sdk.Backend.Beta.Meta;

/// <summary>Extends <see cref="ApiDefaultsOptions"/> with configuration binding for deployment-specific settings.</summary>
public static class ApiDefaultsOptionsExtensions
{
    /// <summary>
    /// Trusts the forwarded headers of the ingress listed under <paramref name="section"/>: its <c>TrustedProxies</c>
    /// addresses and <c>TrustedNetworks</c> CIDR ranges, beside loopback. A missing section or key trusts nothing more.
    /// </summary>
    /// <param name="options">The boot-floor options.</param>
    /// <param name="section">The configuration section, such as <c>Deployment</c>.</param>
    /// <returns>The same <paramref name="options"/> for chaining.</returns>
    /// <example><code>builder.AddApiDefaults(o => o.TrustProxiesFrom(builder.Configuration.GetSection("Deployment")));</code></example>
    public static ApiDefaultsOptions TrustProxiesFrom(this ApiDefaultsOptions options, IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(section);

        foreach (var proxy in section.GetSection(nameof(ApiDefaultsOptions.TrustedProxies)).Get<string[]>() ?? [])
        {
            if (!string.IsNullOrWhiteSpace(proxy)) options.TrustedProxies.Add(proxy.Trim());
        }

        foreach (var network in section.GetSection(nameof(ApiDefaultsOptions.TrustedNetworks)).Get<string[]>() ?? [])
        {
            if (!string.IsNullOrWhiteSpace(network)) options.TrustedNetworks.Add(network.Trim());
        }

        return options;
    }
}
