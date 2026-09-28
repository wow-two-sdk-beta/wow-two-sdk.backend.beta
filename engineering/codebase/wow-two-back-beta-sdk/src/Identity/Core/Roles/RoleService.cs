using System.Security.Claims;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;

/// <summary>Provides role administration: create, rename, delete and find roles, and manage the claims a role grants.</summary>
/// <typeparam name="TRole">The role entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The role repository.</param>
public sealed class RoleService<TRole, TKey>(IRoleRepository<TRole, TKey> repository)
    where TRole : IdentityRole<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Create a role with a unique case-insensitive name.</summary>
    /// <param name="role">The role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        var check = await CheckNameAsync(role, role.Name, cancellationToken);
        if (!check.Succeeded)
            return check;

        role.NormalizedName = role.Name.ToCanonical();
        await repository.CreateAsync(role, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Rename a role, keeping names unique.</summary>
    /// <param name="role">The role.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> RenameAsync(TRole role, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        var check = await CheckNameAsync(role, name, cancellationToken);
        if (!check.Succeeded)
            return check;

        role.Name = name;
        role.NormalizedName = name.ToCanonical();
        await repository.UpdateAsync(role, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Delete a role with its claims and memberships.</summary>
    /// <param name="role">The role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken = default)
    {
        await repository.DeleteAsync(role, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Find a role by id, or null.</summary>
    /// <param name="roleId">The role id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TRole?> FindByIdAsync(TKey roleId, CancellationToken cancellationToken = default)
        => repository.FindByIdAsync(roleId, cancellationToken);

    /// <summary>Find a role by name (case-insensitive), or null.</summary>
    /// <param name="name">The role name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TRole?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => repository.FindByNormalizedNameAsync(name.ToCanonical() ?? string.Empty, cancellationToken);

    /// <summary>The claims the role grants its members.</summary>
    /// <param name="role">The role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        return repository.GetClaimsAsync(role.Id, cancellationToken);
    }

    /// <summary>Grant a claim to every member of the role, from their next sign-in.</summary>
    /// <param name="role">The role.</param>
    /// <param name="claim">The claim.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        await repository.AddClaimAsync(role.Id, claim, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Withdraw a claim from the role; principals issued earlier keep it until they expire.</summary>
    /// <param name="role">The role.</param>
    /// <param name="claim">The claim.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        await repository.RemoveClaimAsync(role.Id, claim, cancellationToken);
        return IdentityResult.Success;
    }

    private async Task<IdentityResult> CheckNameAsync(TRole role, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.RoleNameRequired, "A role name is required.");

        var holder = await repository.FindByNormalizedNameAsync(name.ToCanonical()!, cancellationToken);
        return holder is not null && !holder.Id.Equals(role.Id)
            ? IdentityUserExtensions.Failure(IdentityErrorCodeConstants.DuplicateRoleName, $"Role name '{name}' is already taken.", IdentityUserExtensions.Param("RoleName", name))
            : IdentityResult.Success;
    }
}
