using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>Represents a processed-message marker used for inbox deduplication.</summary>
public sealed class InboxMessageEntity : IHasTableName
{
    /// <summary>The inbox table name.</summary>
    public static string TableName => "inbox_messages";

    /// <summary>The processed message id (primary key / dedupe key).</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>When the message was first seen (UTC).</summary>
    public DateTimeOffset SeenAtUtc { get; set; }
}
