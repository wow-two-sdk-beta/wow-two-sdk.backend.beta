using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Maps the identity entities into an EF Core model — keys, unique lookup indexes, and lengths.</summary>
public static class IdentitySchemaModelBuilderExtensions
{
    /// <summary>
    /// Map the seven identity tables (<c>identity_users</c>, <c>identity_roles</c>, and the user-role / claim / login /
    /// token relations) with normalized-name unique indexes. Call from the app context's <c>OnModelCreating</c> after
    /// <c>base.OnModelCreating(modelBuilder)</c>. Column/table casing comes from <c>UseSnakeCaseNamingConvention()</c>.
    /// </summary>
    /// <typeparam name="TUser">The user entity (derives <see cref="IdentityUser{TKey}"/>).</typeparam>
    /// <typeparam name="TRole">The role entity (derives <see cref="IdentityRole{TKey}"/>).</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="modelBuilder">The model builder.</param>
    public static ModelBuilder ApplyIdentitySchema<TUser, TRole, TKey>(this ModelBuilder modelBuilder)
        where TUser : IdentityUser<TKey>
        where TRole : IdentityRole<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TUser>(user =>
        {
            user.ToTable("identity_users");
            user.HasKey(u => u.Id);
            user.HasIndex(u => u.NormalizedUserName).IsUnique();
            user.HasIndex(u => u.NormalizedEmail);
            user.Property(u => u.UserName).HasMaxLength(256);
            user.Property(u => u.NormalizedUserName).HasMaxLength(256);
            user.Property(u => u.Email).HasMaxLength(320);
            user.Property(u => u.NormalizedEmail).HasMaxLength(320);
            user.Property(u => u.PhoneNumber).HasMaxLength(32);
            user.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
        });

        modelBuilder.Entity<TRole>(role =>
        {
            role.ToTable("identity_roles");
            role.HasKey(r => r.Id);
            role.HasIndex(r => r.NormalizedName).IsUnique();
            role.Property(r => r.Name).HasMaxLength(256);
            role.Property(r => r.NormalizedName).HasMaxLength(256);
            role.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();
        });

        modelBuilder.Entity<IdentityUserRole<TKey>>(userRole =>
        {
            userRole.ToTable("identity_user_roles");
            userRole.HasKey(ur => new { ur.UserId, ur.RoleId });
        });

        modelBuilder.Entity<IdentityUserClaim<TKey>>(userClaim =>
        {
            userClaim.ToTable("identity_user_claims");
            userClaim.HasKey(uc => uc.Id);
            userClaim.HasIndex(uc => uc.UserId);
        });

        modelBuilder.Entity<IdentityRoleClaim<TKey>>(roleClaim =>
        {
            roleClaim.ToTable("identity_role_claims");
            roleClaim.HasKey(rc => rc.Id);
            roleClaim.HasIndex(rc => rc.RoleId);
        });

        modelBuilder.Entity<IdentityUserLogin<TKey>>(login =>
        {
            login.ToTable("identity_user_logins");
            login.HasKey(l => new { l.LoginProvider, l.ProviderKey });
            login.Property(l => l.LoginProvider).HasMaxLength(128);
            login.Property(l => l.ProviderKey).HasMaxLength(128);
            login.HasIndex(l => l.UserId);
        });

        modelBuilder.Entity<IdentityUserToken<TKey>>(token =>
        {
            token.ToTable("identity_user_tokens");
            token.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
            token.Property(t => t.LoginProvider).HasMaxLength(128);
            token.Property(t => t.Name).HasMaxLength(128);
        });

        return modelBuilder;
    }
}
