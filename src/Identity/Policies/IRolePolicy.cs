namespace WoW.Two.Sdk.Backend.Beta.Identity.Policies;

/// <summary>Decides whether a role may enter a scope; both sides are consumer-defined strings.</summary>
public interface IRolePolicy
{
    /// <summary>Returns <c>true</c> when <paramref name="role"/> is allowed into <paramref name="scope"/>.</summary>
    /// <param name="role">The role carried by the authenticated subject (e.g. <c>"admin"</c>).</param>
    /// <param name="scope">The scope being entered (e.g. <c>"crm"</c>).</param>
    bool IsAllowed(string role, string scope);
}
