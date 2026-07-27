using EntityFrameworkCore.Projectables.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Projectables;

/// <summary>Convenience extensions for <c>EFCore.Projectables</c>.</summary>
public static class ProjectablesExtensions
{
    /// <summary>Enables <c>[Projectable]</c>-attributed computed properties on the DbContext.</summary>
    /// <param name="builder">The DbContext options builder to configure.</param>
    public static DbContextOptionsBuilder UseProjectablesConventional(this DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseProjectables();
    }
}
