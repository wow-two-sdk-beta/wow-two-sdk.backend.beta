using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Configuration;

/// <summary>
/// Extends a configuration manager with an explicit environment contract: only listed variables reach configuration,
/// each under the key it is mapped to, so an unrelated variable in the deployment can never override a setting.
/// </summary>
public static class EnvironmentAliasExtensions
{
    /// <summary>The hosting keys ASP.NET Core reads from <c>ASPNETCORE_</c> variables, kept when the sources are removed.</summary>
    public static readonly IReadOnlyList<string> HostingKeys = ["urls", "http_ports", "https_ports"];

    /// <summary>
    /// Replaces every environment-variable configuration source with the listed <paramref name="aliases"/>: each maps a
    /// variable name to a configuration key, and a set, non-empty variable overrides that key. The hosting keys already
    /// read from the environment are kept. Sources added later, such as test overrides, still win.
    /// </summary>
    /// <param name="configuration">The configuration manager, usually <c>builder.Configuration</c>.</param>
    /// <param name="aliases">The supported variables, each mapped to its configuration key.</param>
    /// <returns>The same <paramref name="configuration"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
    /// builder.Configuration.UseEnvironmentAliases(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["DB_CONNECTION"] = "DB_CONNECTION",
    ///     ["GOOGLE_CLIENT_ID"] = "Auth:Google:ClientId",
    /// });
    /// </code>
    /// </example>
    public static IConfigurationManager UseEnvironmentAliases(
        this IConfigurationManager configuration,
        IReadOnlyDictionary<string, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(aliases);

        var hosting = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in HostingKeys)
        {
            if (configuration[key] is { } value) hosting[key] = value;
        }

        foreach (var source in configuration.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToArray())
        {
            configuration.Sources.Remove(source);
        }

        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (variable, key) in aliases)
        {
            if (Environment.GetEnvironmentVariable(variable) is { } value && !string.IsNullOrWhiteSpace(value))
                overrides[key] = value;
        }

        configuration.AddInMemoryCollection(hosting);
        configuration.AddInMemoryCollection(overrides);
        return configuration;
    }
}
