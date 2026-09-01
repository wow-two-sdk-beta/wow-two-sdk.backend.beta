using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>EF mapping for <see cref="OutboxMessageEntity"/> — DDL is owned by the bespoke migrator; this maps the CLR type over it.</summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(OutboxMessageEntity.TableName);
        builder.HasKey(entity => entity.Id);
    }
}
