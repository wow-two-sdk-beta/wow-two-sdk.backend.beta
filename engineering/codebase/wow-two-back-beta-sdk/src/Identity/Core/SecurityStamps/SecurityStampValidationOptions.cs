namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.SecurityStamps;

/// <summary>Holds how often a principal's security stamp is compared with the stored one.</summary>
public sealed record SecurityStampValidationOptions
{
    /// <summary>How long a user's stored stamp is cached per host; zero reads it on every request. Default 1 minute.</summary>
    public TimeSpan ValidationInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Reject a principal that carries a user id but no stamp claim. Default false, for principals issued elsewhere.</summary>
    public bool RejectPrincipalsWithoutStamp { get; set; }
}
