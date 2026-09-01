using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Data.Errors;

/// <summary>Provides registration for the data-layer exception mapping rule.</summary>
public static class DbExceptionMappingServiceCollectionExtensions
{
    /// <summary>Registers the SDK's <see cref="DbExceptionMappingRule"/> (and the <see cref="IExceptionMapper"/> it contributes to) so Npgsql/EF exceptions map to their <see cref="AppError"/>. Auto-wired by <c>AddPostgresPersistence</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddDbExceptionMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddExceptionMappingRule<DbExceptionMappingRule>();
    }
}
