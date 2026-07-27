using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Base <see cref="DbContext"/> for consumer contexts — applies entity-type configurations and SDK conventions, and increments <see cref="IVersioned"/> tokens on save.</summary>
/// <remarks>Override <see cref="OnModelCreating"/> in derived classes and call <c>base.OnModelCreating(modelBuilder)</c> first.</remarks>
public abstract class AppDbContextBase : DbContext
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
            if (entry is { State: EntityState.Modified, Entity: IVersioned versioned })
                versioned.Version++;
    }
}
