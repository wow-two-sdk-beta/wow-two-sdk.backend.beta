using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Opts the account HTTP API's email support into an identity registration.</summary>
public static class AccountEndpointIdentityBuilderExtensions
{
    /// <summary>
    /// Register <see cref="UserAccountMailService{TUser,TKey}"/> with <see cref="UserAccountEmailSettings"/> bound live from
    /// the container's <c>UserAccounts:Emails</c> section; map the API with <c>MapUserAccountEndpoints</c>.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddAccountEndpoints<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IOptionsChangeTokenSource<UserAccountEmailSettings>)))
        {
            services.AddOptions<UserAccountEmailSettings>()
                .Configure<IServiceProvider>((settings, provider) =>
                    provider.GetService<IConfiguration>()?.GetSection(UserAccountEmailSettings.SectionName).Bind(settings));
            services.AddSingleton<IOptionsChangeTokenSource<UserAccountEmailSettings>>(provider =>
                new ConfigurationChangeTokenSource<UserAccountEmailSettings>(
                    (IConfiguration?)provider.GetService<IConfiguration>()?.GetSection(UserAccountEmailSettings.SectionName)
                        ?? new ConfigurationBuilder().Build()));
        }

        services.TryAddScoped<UserAccountMailService<TUser, TKey>>();
        return builder;
    }
}
