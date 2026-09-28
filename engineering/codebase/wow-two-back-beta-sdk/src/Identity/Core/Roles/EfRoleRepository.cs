using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;

/// <summary>Accesses roles and role claims through the context hosting the identity schema.</summary>
/// <typeparam name="TRole">The role entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
public sealed class EfRoleRepository<TRole, TKey>(DbContext context) : IRoleRepository<TRole, TKey>
    where TRole : IdentityRole<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private DbSet<TRole> Roles => context.Set<TRole>();

    private DbSet<IdentityRoleClaim<TKey>> RoleClaims => context.Set<IdentityRoleClaim<TKey>>();

    /// <inheritdoc />
    public async Task CreateAsync(TRole role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TRole role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (context.Entry(role).State == EntityState.Detached)
            Roles.Update(role);

        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TRole role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        var roleId = role.Id;
        context.RemoveRange(await RoleClaims.Where(c => c.RoleId.Equals(roleId)).ToListAsync(cancellationToken));
        context.RemoveRange(await context.Set<IdentityUserRole<TKey>>().Where(r => r.RoleId.Equals(roleId)).ToListAsync(cancellationToken));
        Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TRole?> FindByIdAsync(TKey roleId, CancellationToken cancellationToken = default)
        => await Roles.FindAsync([roleId], cancellationToken);

    /// <inheritdoc />
    public Task<TRole?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        => Roles.FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Claim>> GetClaimsAsync(TKey roleId, CancellationToken cancellationToken = default)
        => await RoleClaims
            .Where(c => c.RoleId.Equals(roleId))
            .Select(c => new Claim(c.ClaimType!, c.ClaimValue ?? string.Empty))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddClaimAsync(TKey roleId, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        RoleClaims.Add(new IdentityRoleClaim<TKey> { RoleId = roleId, ClaimType = claim.Type, ClaimValue = claim.Value });
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveClaimAsync(TKey roleId, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        context.RemoveRange(await RoleClaims
            .Where(c => c.RoleId.Equals(roleId) && c.ClaimType == claim.Type && c.ClaimValue == claim.Value)
            .ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }
}
