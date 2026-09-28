using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.SecurityStamps;

/// <summary>Opts security-stamp revocation into an identity registration.</summary>
public static class SecurityStampIdentityBuilderExtensions
{
    /// <summary>
    /// Reject cookie and JWT bearer principals whose security stamp no longer matches the stored user's. Hooks every
    /// cookie scheme's <c>OnValidatePrincipal</c> and every bearer scheme's <c>OnTokenValidated</c>.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <param name="configure">Optional interval and missing-stamp behavior.</param>
    public static IdentityBuilder<TUser, TKey> AddSecurityStampValidation<TUser, TKey>(
        this IdentityBuilder<TUser, TKey> builder,
        Action<SecurityStampValidationOptions>? configure = null)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        services.AddValidatedOptions(
            configure,
            options => options.Validate(o => o.ValidationInterval >= TimeSpan.Zero, "SecurityStampValidationOptions.ValidationInterval must not be negative."));
        services.AddMemoryCache();
        services.TryAddScoped<SecurityStampValidator<TUser, TKey>>();

        services.PostConfigureAll<CookieAuthenticationOptions>(cookie =>
        {
            var previous = cookie.Events.OnValidatePrincipal;
            cookie.Events.OnValidatePrincipal = async context =>
            {
                await previous(context);
                if (context.Principal is null)
                    return;

                var validator = context.HttpContext.RequestServices.GetRequiredService<SecurityStampValidator<TUser, TKey>>();
                if (!await validator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(context.Scheme.Name);
                }
            };
        });

        services.PostConfigureAll<JwtBearerOptions>(bearer =>
        {
            bearer.Events ??= new JwtBearerEvents();
            var previous = bearer.Events.OnTokenValidated;
            bearer.Events.OnTokenValidated = async context =>
            {
                await previous(context);
                if (context.Principal is null || context.Result is { Failure: not null })
                    return;

                var validator = context.HttpContext.RequestServices.GetRequiredService<SecurityStampValidator<TUser, TKey>>();
                if (!await validator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
                    context.Fail("The security stamp is no longer valid.");
            };
        });

        return builder;
    }
}
