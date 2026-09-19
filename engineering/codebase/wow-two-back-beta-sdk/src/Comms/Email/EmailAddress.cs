namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>An email address with an optional display name.</summary>
public sealed record EmailAddress
{
    /// <summary>The address (<c>user@example.com</c>).</summary>
    public required string Address { get; init; }

    /// <summary>Optional display name.</summary>
    public string? DisplayName { get; init; }
}
