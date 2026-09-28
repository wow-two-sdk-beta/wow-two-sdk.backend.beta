using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Specs;
using WoW.Two.Sdk.Backend.Beta.Data.Specs;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Base <see cref="DbContext"/> for consumer contexts — applies entity-type configurations, registered entity specs and SDK conventions; increments <see cref="IVersioned"/> counters and replaces spec stamps on save.</summary>
/// <remarks>Override <see cref="OnModelCreating"/> in derived classes and call <c>base.OnModelCreating(modelBuilder)</c> first.</remarks>
public abstract partial class AppDbContextBase : DbContext
{
    /// <summary>Initializes the context with the given options.</summary>
    /// <param name="options">The options configuring this context.</param>
    protected AppDbContextBase(DbContextOptions options) : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> in the concrete context's assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Apply registered provider-neutral entity specs to the entities this context maps.
        ApplyRegisteredEntitySpecs(modelBuilder);

        // Apply SDK contract-driven conventions (soft-delete filter, IVersioned token).
        modelBuilder.ApplyConventions();

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ConfigureConventionsCore(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>Override to apply additional model conventions — value converters, default precision, and similar.</summary>
    /// <param name="configurationBuilder">The model configuration builder.</param>
    protected virtual void ConfigureConventionsCore(ModelConfigurationBuilder configurationBuilder)
    {
        // Default: no-op. Provider preset packages may override.
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IncrementConcurrencyVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IncrementConcurrencyVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void IncrementConcurrencyVersions()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
                continue;

            if (entry.Entity is IVersioned versioned)
                versioned.Version++;

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.FindAnnotation(EntitySpecModelBuilderExtensions.ConcurrencyStampAnnotation) is not null)
                    property.CurrentValue = Guid.NewGuid().ToString("N");
            }
        }
    }

    /// <summary>Maps the <see cref="EntitySpecRegistry"/> registered by <c>AddEntitySpecs</c>, when there is one.</summary>
    private void ApplyRegisteredEntitySpecs(ModelBuilder modelBuilder)
    {
        var services = this.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;
        if (services?.GetService(typeof(EntitySpecRegistry)) is not EntitySpecRegistry registry)
            return;

        var options = (services.GetService(typeof(IOptions<EntitySpecOptions>)) as IOptions<EntitySpecOptions>)?.Value ?? new EntitySpecOptions();
        var logger = (services.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger<AppDbContextBase>();
        modelBuilder.ApplyEntitySpecs(registry, Database.ProviderName, options.Unsupported, feature =>
        {
            if (logger is not null)
                LogSpecFeatureSkipped(logger, feature);
        });
    }

    [LoggerMessage(EventId = 3310, Level = LogLevel.Warning, Message = "Entity spec feature skipped: {Feature}")]
    private static partial void LogSpecFeatureSkipped(ILogger logger, string feature);
}
