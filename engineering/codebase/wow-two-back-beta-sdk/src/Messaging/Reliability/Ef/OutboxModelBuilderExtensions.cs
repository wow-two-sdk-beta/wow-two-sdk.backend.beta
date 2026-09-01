using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>Model-builder extension registering the outbox entity mapping.</summary>
public static class OutboxModelBuilderExtensions
{
    /// <summary>Map <see cref="OutboxMessageEntity"/> into the model. Call from the context's <c>OnModelCreating</c> when using the EF outbox.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static ModelBuilder ApplyOutboxModel(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        return modelBuilder;
    }
}
