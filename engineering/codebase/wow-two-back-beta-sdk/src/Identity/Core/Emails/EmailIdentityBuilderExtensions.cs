using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Emails;

/// <summary>Opts the email slice into an identity registration.</summary>
public static class EmailIdentityBuilderExtensions
{
    /// <summary>Register <see cref="UserEmailService{TUser,TKey}"/>; needs <c>.AddUserTokens(...)</c> for its tokens.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddEmailConfirmation<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddScoped<UserEmailService<TUser, TKey>>();
        return builder;
    }
}
