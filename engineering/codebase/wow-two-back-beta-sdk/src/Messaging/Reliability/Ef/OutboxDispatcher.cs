using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// Drains pending <see cref="OutboxMessageEntity"/> rows to the event bus: claim a batch (via <see cref="IOutboxClaimRepository"/>),
/// resolve the CLR type (via <see cref="IMessageTypeMapper"/>), deserialize the payload (via <see cref="IMessageSerializer"/>),
/// publish, then stamp <c>ProcessedOnUtc</c> (or bump <c>Attempts</c> + record the error).
/// </summary>
/// <typeparam name="TContext">The application's DbContext.</typeparam>
internal sealed partial class OutboxDispatcher<TContext>(
    TContext context,
    OutboxEventPublisher publisher,
    IOutboxClaimRepository claimStrategy,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    TimeProvider timeProvider,
    OutboxDispatcherOptions options,
    ILogger<OutboxDispatcher<TContext>> logger) : IOutboxDispatcher
    where TContext : DbContext
{
    public async ValueTask<int> DispatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var opt = options;
        var pending = await claimStrategy.ClaimPendingAsync(context, batchSize, cancellationToken);

        var dispatched = 0;
        foreach (var row in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Stable token first, assembly-qualified-name fallback — the resolver owns both (and caches the fallback).
            var eventType = string.IsNullOrEmpty(row.Type) ? null : typeResolver.ResolveType(row.Type);
            if (eventType is null)
            {
                MarkFailed(row, $"Unresolved event type '{row.Type}'.", opt);
                LogTypeUnresolved(row.Id, row.Type);
                continue;
            }

            // Fail a row whose content type does not match the registered serializer; empty means unchecked.
            if (!string.IsNullOrEmpty(row.ContentType) && !string.Equals(row.ContentType, serializer.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                MarkFailed(row, $"Payload content type '{row.ContentType}' does not match the registered serializer '{serializer.ContentType}'.", opt);
                LogContentTypeMismatch(row.Id, row.ContentType, serializer.ContentType);
                continue;
            }

            try
            {
                if (serializer.Deserialize(row.Payload, eventType).IsFailure(out var decodeError, out var @event))
                {
                    MarkFailed(row, decodeError.Message, opt);
                    continue;
                }

                // Read headers_json back so the staged headers, W3C trace context above all, reach the wire.
                var staged = OutboxDispatchHeaderMapper.Read(row.HeadersJson);

                // Re-root the publish in the producer's trace — the bus stamps traceparent from the ambient Activity.
                using (OutboxDispatchHeaderMapper.StartTraceContinuation(staged))
                {
                    await publisher.PublishAsync(eventType, @event, OutboxDispatchHeaderMapper.ToPublishOptions(staged, row.Id), cancellationToken);
                }

                row.ProcessedOnUtc = timeProvider.GetUtcNow();
                row.Error = null;
                dispatched++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                MarkFailed(row, ex.Message, opt);
                LogDispatchFailed(ex, row.Id);
            }
        }

        if (pending.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        return dispatched;
    }

    /// <inheritdoc />
    public async ValueTask<int> PruneProcessedAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - retention;
        return await context.Set<OutboxMessageEntity>()
            .Where(row => row.ProcessedOnUtc != null && row.ProcessedOnUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Bump attempts; at the cap, stamp ProcessedOnUtc so the poison row stops re-selecting (Error retained).
    private void MarkFailed(OutboxMessageEntity row, string error, OutboxDispatcherOptions opt)
    {
        row.Attempts++;
        row.Error = error;
        if (row.Attempts >= opt.MaxDispatchAttempts)
            row.ProcessedOnUtc = timeProvider.GetUtcNow();
    }

    [LoggerMessage(EventId = 6201, Level = LogLevel.Error, Message = "Outbox dispatch failed for message {Id}")]
    private partial void LogDispatchFailed(Exception exception, Guid id);

    [LoggerMessage(EventId = 6202, Level = LogLevel.Warning, Message = "Outbox message {Id} has unresolved event type {Type}")]
    private partial void LogTypeUnresolved(Guid id, string type);

    [LoggerMessage(EventId = 6204, Level = LogLevel.Error, Message = "Outbox message {Id} was staged as {RowContentType} but the registered serializer produces {SerializerContentType}")]
    private partial void LogContentTypeMismatch(Guid id, string rowContentType, string serializerContentType);
}
