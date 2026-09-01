using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>EF mapping for <see cref="InboxMessageEntity"/> — DDL is owned by the bespoke migrator; this maps the CLR type over it.</summary>
internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<InboxMessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(InboxMessageEntity.TableName);
        builder.HasKey(entity => entity.MessageId);
    }
}
