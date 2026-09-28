using WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.PersonalData;

/// <summary>Exports the names of the user's roles as the <c>roles</c> section; registered by <c>AddRoles</c>.</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TRole">The role entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="roles">Reads the user's roles.</param>
public sealed class RolePersonalDataExporter<TUser, TRole, TKey>(UserRoleService<TUser, TRole, TKey> roles) : IPersonalDataExporter<TUser>
    where TUser : IdentityUser<TKey>
    where TRole : IdentityRole<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <inheritdoc />
    public string Section => "roles";

    /// <inheritdoc />
    public async Task<object?> ExportAsync(TUser user, CancellationToken cancellationToken = default)
        => await roles.GetRolesAsync(user, cancellationToken);
}
