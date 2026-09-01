using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>Model-builder extension registering the inbox entity mapping.</summary>
public static class InboxModelBuilderExtensions
{
    /// <summary>Map <see cref="InboxMessageEntity"/> into the model. Call from the context's <c>OnModelCreating</c> when using the EF inbox.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static ModelBuilder ApplyInboxModel(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        return modelBuilder;
    }
}
