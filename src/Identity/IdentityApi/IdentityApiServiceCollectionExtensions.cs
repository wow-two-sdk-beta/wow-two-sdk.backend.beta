using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.IdentityApi;

/// <summary>
/// ASP.NET Core Identity registration with bearer-token API endpoints.
/// </summary>
public static class IdentityApiServiceCollectionExtensions
{
    /// <summary>
    /// Register Identity with `IdentityUser` over the supplied EF Core context, plus bearer-token endpoints.
    /// After build, call `app.MapIdentityApi&lt;IdentityUser&gt;()`.
    /// </summary>
    public static IServiceCollection AddIdentityApiEndpoints<TContext>(this IServiceCollection services)
        where TContext : DbContext
        => services.AddIdentityApiEndpoints<IdentityUser, TContext>();

    /// <summary>
    /// Register Identity with a custom user type and EF Core context, plus bearer-token endpoints.
    /// </summary>
    public static IServiceCollection AddIdentityApiEndpoints<TUser, TContext>(this IServiceCollection services)
        where TUser : class, new()
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddIdentityCore<TUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 12;
                o.Password.RequireDigit = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireNonAlphanumeric = false;
                o.SignIn.RequireConfirmedEmail = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<TContext>()
            .AddApiEndpoints();

        return services;
    }
}
