namespace WoW.Two.Sdk.Backend.Beta.Identity.Policies;

/// <summary>
/// Decides whether a role may enter a scope (service, area, feature). Both sides are plain strings —
/// consumers define their own role and scope vocabulary.
/// </summary>
public interface IRolePolicy
{
    /// <summary>Returns <c>true</c> when <paramref name="role"/> is allowed into <paramref name="scope"/>.</summary>
    /// <param name="role">The role carried by the authenticated subject (e.g. <c>"admin"</c>).</param>
    /// <param name="scope">The scope being entered (e.g. <c>"crm"</c>).</param>
    bool IsAllowed(string role, string scope);
}
