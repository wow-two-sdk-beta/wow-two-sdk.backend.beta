namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>An email address with an optional display name.</summary>
/// <param name="Address">The address (<c>user@example.com</c>).</param>
/// <param name="DisplayName">Optional display name.</param>
public sealed record EmailAddress(string Address, string? DisplayName = null);
