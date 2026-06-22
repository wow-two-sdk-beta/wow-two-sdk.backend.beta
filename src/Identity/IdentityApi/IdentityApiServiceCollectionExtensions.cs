using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.IdentityApi;

/// <summary>Provides ASP.NET Core Identity registration with bearer-token API endpoints.</summary>
public static class IdentityApiServiceCollectionExtensions
{
    /// <summary>Registers Identity with <c>IdentityUser</c> over the supplied EF Core context plus bearer-token endpoints; after build, call <c>app.MapIdentityApi&lt;IdentityUser&gt;()</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddIdentityApiEndpoints<TContext>(this IServiceCollection services)
        where TContext : DbContext
        => services.AddIdentityApiEndpoints<IdentityUser, TContext>();

    /// <summary>Registers Identity with a custom user type and EF Core context plus bearer-token endpoints.</summary>
    /// <param name="services">The service collection to configure.</param>
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
