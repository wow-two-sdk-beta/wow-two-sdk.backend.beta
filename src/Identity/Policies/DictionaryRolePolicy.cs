using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Policies;

/// <summary>
/// Default <see cref="IRolePolicy"/> backed by the <see cref="RolePolicyOptions.Map"/> dictionary.
/// Unknown scopes deny everyone.
/// </summary>
public sealed class DictionaryRolePolicy : IRolePolicy
{
    private readonly IDictionary<string, IReadOnlySet<string>> _map;

    /// <summary>Creates the policy from configured options.</summary>
    /// <param name="options">The scope → allowed-roles map.</param>
    public DictionaryRolePolicy(IOptions<RolePolicyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _map = options.Value.Map;
    }

    /// <inheritdoc />
    public bool IsAllowed(string role, string scope)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(scope);
        return _map.TryGetValue(scope, out var roles) && roles.Contains(role);
    }
}
