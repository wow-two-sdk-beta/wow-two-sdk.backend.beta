using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Holds the claim types the principal factory writes and the security-stamp check reads.</summary>
public sealed record IdentityClaimOptions
{
    /// <summary>Claim carrying the user id. Default <see cref="ClaimTypes.NameIdentifier"/>; JWT hosts often use <c>sub</c>.</summary>
    public string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>Claim carrying the user name. Default <see cref="ClaimTypes.Name"/>.</summary>
    public string UserNameClaimType { get; set; } = ClaimTypes.Name;

    /// <summary>Claim carrying the email. Default <see cref="ClaimTypes.Email"/>.</summary>
    public string EmailClaimType { get; set; } = ClaimTypes.Email;

    /// <summary>Claim carrying each role. Default <see cref="ClaimTypes.Role"/>.</summary>
    public string RoleClaimType { get; set; } = ClaimTypes.Role;

    /// <summary>Claim carrying the security stamp. Default <c>AspNet.Identity.SecurityStamp</c>, as ASP.NET Identity writes it.</summary>
    public string SecurityStampClaimType { get; set; } = "AspNet.Identity.SecurityStamp";
}
