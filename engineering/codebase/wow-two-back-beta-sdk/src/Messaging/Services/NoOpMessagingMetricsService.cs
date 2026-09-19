using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Services;

/// <summary>
/// Provides no-op messaging telemetry recording.
/// Register it before the transport (<c>services.AddSingleton&lt;IMessagingMetricsService&gt;(NoOpMessagingMetricsService.Instance)</c>)
/// and the default registration, which is <c>TryAdd</c>-based, stands down.
/// </summary>
public sealed class NoOpMessagingMetricsService : IMessagingMetricsService
{
    /// <summary>Holds the shared instance — the type is stateless.</summary>
    public static readonly NoOpMessagingMetricsService Instance = new();

    void IMessagingMetricsService.RecordPublished(string destination, Type eventType) { }

    void IMessagingMetricsService.RecordConsumed(string destination, Type eventType, ConsumeOutcome outcome) { }

    void IMessagingMetricsService.RecordConsumeDuration(string destination, Type eventType, TimeSpan elapsed) { }

    void IMessagingMetricsService.RecordDeadLettered(string destination, Type eventType, Exception? exception) { }

    void IMessagingMetricsService.RecordRetried(string destination, Type eventType) { }

    IDisposable IMessagingMetricsService.TrackInFlight(Func<int> probe) => NullRegistration.Instance;

    private sealed class NullRegistration : IDisposable
    {
        public static readonly NullRegistration Instance = new();

        public void Dispose() { }
    }
}
