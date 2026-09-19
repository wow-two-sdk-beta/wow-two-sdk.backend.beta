using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>In-memory scheduler — delays via the time provider then enqueues onto the channel.</summary>
internal sealed partial class InMemoryDelayedDeliveryService(InMemoryEventChannel channel, TimeProvider timeProvider, ILogger<InMemoryDelayedDeliveryService> logger) : IDelayedDeliveryService
{
    public ValueTask ScheduleAsync(EventEnvelopeModel envelope, DateTimeOffset notBeforeUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var delay = notBeforeUtc - timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
            return channel.Writer.WriteAsync(envelope, cancellationToken);

        _ = DelayThenEnqueueAsync(envelope, delay, cancellationToken);
        return ValueTask.CompletedTask;
    }

    private async Task DelayThenEnqueueAsync(EventEnvelopeModel envelope, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, cancellationToken);
            await channel.Writer.WriteAsync(envelope, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // host shutting down — drop the scheduled delivery
        }
        catch (Exception ex)
        {
            LogScheduledEnqueueFailed(ex, envelope.MessageId);
        }
    }

    [LoggerMessage(EventId = 6002, Level = LogLevel.Error, Message = "Scheduled enqueue failed for message {MessageId}")]
    private partial void LogScheduledEnqueueFailed(Exception exception, string messageId);
}
