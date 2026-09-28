using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Phones;

/// <summary>Opts the phone slice into an identity registration.</summary>
public static class PhoneIdentityBuilderExtensions
{
    /// <summary>Register <see cref="UserPhoneService{TUser,TKey}"/> over <see cref="IOtpService"/>, registering the OTP service when absent.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddPhoneNumbers<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddOtpService();
        builder.Services.TryAddScoped<UserPhoneService<TUser, TKey>>();
        return builder;
    }
}
