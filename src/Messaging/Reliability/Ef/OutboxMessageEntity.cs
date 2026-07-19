using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>A staged outgoing event persisted in the transactional outbox table (<c>outbox_messages</c>).</summary>
public sealed class OutboxMessageEntity : IKeyedEntity<Guid>, IHasTableName
{
    /// <summary>The outbox table name.</summary>
    public static string TableName => "outbox_messages";

    /// <summary>Row id (also the transport message id).</summary>
    public Guid Id { get; set; }

    /// <summary>Logical event type/name used to resolve the CLR type on dispatch.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Serialized event body.</summary>
    public byte[] Payload { get; set; } = [];

    /// <summary>
    /// Format of <see cref="Payload"/> — the staging <c>IMessageSerializer</c>'s content type (e.g. <c>application/json</c>),
    /// mirroring the content type the transport carries on the wire envelope. Dispatch compares it against the registered
    /// serializer so a payload written in another format fails loudly instead of deserializing to garbage. Empty on rows
    /// written before the column existed (comparison is then skipped).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>When the event was produced (UTC).</summary>
    public DateTimeOffset OccurredOnUtc { get; set; }

    /// <summary>Serialized headers (JSON) attached on dispatch.</summary>
    public string HeadersJson { get; set; } = "{}";

    /// <summary>When the row was successfully dispatched; null while pending.</summary>
    public DateTimeOffset? ProcessedOnUtc { get; set; }

    /// <summary>Dispatch attempt count.</summary>
    public int Attempts { get; set; }

    /// <summary>Last dispatch error, if any.</summary>
    public string? Error { get; set; }
}

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
