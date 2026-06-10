namespace WoW.Two.Sdk.Backend.Beta.Identity.Policies;

/// <summary>
/// Scope → allowed-roles map backing <see cref="DictionaryRolePolicy"/>.
/// </summary>
public sealed class RolePolicyOptions
{
    /// <summary>
    /// Allowed roles per scope. A scope absent from the map allows no one.
    /// Example: <c>o.Map["crm"] = new HashSet&lt;string&gt; { "agent", "admin" };</c>
    /// </summary>
    public IDictionary<string, IReadOnlySet<string>> Map { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
}
