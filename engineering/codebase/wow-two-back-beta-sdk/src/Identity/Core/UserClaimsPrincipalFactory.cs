using System.Globalization;
using System.Security.Claims;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.UserClaims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>
/// Creates the <see cref="ClaimsPrincipal"/> a sign-in issues for a user: id, user name, email and security stamp, plus
/// role names, role claims and user claims when those slices are registered. Claim types follow
/// <see cref="IdentityCoreOptions.Claims"/>, so a cookie and a JWT built from it read back the same way.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="options">Identity options carrying the claim types.</param>
/// <param name="roles">The user-role repository; null without the roles slice.</param>
/// <param name="userClaims">The user-claim repository; null without the user-claims slice.</param>
public sealed class UserClaimsPrincipalFactory<TUser, TKey>(
    IdentityCoreOptions options,
    IUserRoleRepository<TKey>? roles = null,
    IUserClaimRepository<TKey>? userClaims = null)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The authentication type given to identities unless the caller names one.</summary>
    public const string DefaultAuthenticationType = "Identity.Application";

    /// <summary>Create the principal for <paramref name="user"/>.</summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="authenticationType">The identity's authentication type; must be non-empty for the identity to count as authenticated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ClaimsPrincipal> CreateAsync(TUser user, string authenticationType = DefaultAuthenticationType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationType);

        var types = options.Claims;
        var identity = new ClaimsIdentity(authenticationType, types.UserNameClaimType, types.RoleClaimType);
        identity.AddClaim(new Claim(types.UserIdClaimType, Convert.ToString(user.Id, CultureInfo.InvariantCulture) ?? string.Empty));
        if (!string.IsNullOrEmpty(user.UserName))
            identity.AddClaim(new Claim(types.UserNameClaimType, user.UserName));
        if (!string.IsNullOrEmpty(user.Email))
            identity.AddClaim(new Claim(types.EmailClaimType, user.Email));
        if (!string.IsNullOrEmpty(user.SecurityStamp))
            identity.AddClaim(new Claim(types.SecurityStampClaimType, user.SecurityStamp));

        if (roles is not null)
        {
            foreach (var role in await roles.GetRoleNamesAsync(user.Id, cancellationToken))
                identity.AddClaim(new Claim(types.RoleClaimType, role));
            identity.AddClaims(await roles.GetRoleClaimsAsync(user.Id, cancellationToken));
        }

        if (userClaims is not null)
            identity.AddClaims(await userClaims.GetAsync(user.Id, cancellationToken));

        return new ClaimsPrincipal(identity);
    }
}
