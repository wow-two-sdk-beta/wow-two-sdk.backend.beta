using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Logins;

/// <summary>Opts external logins into an identity registration.</summary>
public static class LoginIdentityBuilderExtensions
{
    /// <summary>Register <see cref="UserLoginService{TUser,TKey}"/> and, after <c>AddEntityFrameworkStores</c>, its EF repository.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddExternalLogins<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.ContextFactory is { } context)
            builder.Services.TryAddScoped<IUserLoginRepository<TKey>>(serviceProvider => new EfUserLoginRepository<TKey>(context(serviceProvider)));

        builder.Services.TryAddScoped<UserLoginService<TUser, TKey>>();
        return builder;
    }
}
