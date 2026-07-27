namespace WoW.Two.Sdk.Backend.Beta.Identity.Policies;

/// <summary>Scope → allowed-roles map backing <see cref="DictionaryRolePolicy"/>.</summary>
public sealed class RolePolicyOptions
{
    /// <summary>Gets the allowed roles per scope; a scope absent from the map allows no one (e.g. <c>o.Map["crm"] = new HashSet&lt;string&gt; { "agent", "admin" };</c>).</summary>
    public IDictionary<string, IReadOnlySet<string>> Map { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
}
