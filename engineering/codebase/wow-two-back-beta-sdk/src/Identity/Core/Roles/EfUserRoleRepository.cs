using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;

/// <summary>Accesses role memberships through the context hosting the identity schema.</summary>
/// <typeparam name="TRole">The role entity joined for names and claims.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
public sealed class EfUserRoleRepository<TRole, TKey>(DbContext context) : IUserRoleRepository<TKey>
    where TRole : IdentityRole<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private DbSet<IdentityUserRole<TKey>> Memberships => context.Set<IdentityUserRole<TKey>>();

    /// <inheritdoc />
    public async Task AddAsync(TKey userId, TKey roleId, CancellationToken cancellationToken = default)
    {
        Memberships.Add(new IdentityUserRole<TKey> { UserId = userId, RoleId = roleId });
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(TKey userId, TKey roleId, CancellationToken cancellationToken = default)
    {
        context.RemoveRange(await Memberships.Where(m => m.UserId.Equals(userId) && m.RoleId.Equals(roleId)).ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ContainsAsync(TKey userId, TKey roleId, CancellationToken cancellationToken = default)
        => Memberships.AnyAsync(m => m.UserId.Equals(userId) && m.RoleId.Equals(roleId), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(TKey userId, CancellationToken cancellationToken = default)
        => await (from membership in Memberships
                  join role in context.Set<TRole>() on membership.RoleId equals role.Id
                  where membership.UserId.Equals(userId) && role.Name != null
                  orderby role.Name
                  select role.Name!).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Claim>> GetRoleClaimsAsync(TKey userId, CancellationToken cancellationToken = default)
        => await (from membership in Memberships
                  join claim in context.Set<IdentityRoleClaim<TKey>>() on membership.RoleId equals claim.RoleId
                  where membership.UserId.Equals(userId) && claim.ClaimType != null
                  select new Claim(claim.ClaimType!, claim.ClaimValue ?? string.Empty)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TKey>> GetUserIdsAsync(TKey roleId, CancellationToken cancellationToken = default)
        => await Memberships.Where(m => m.RoleId.Equals(roleId)).Select(m => m.UserId).ToListAsync(cancellationToken);
}
