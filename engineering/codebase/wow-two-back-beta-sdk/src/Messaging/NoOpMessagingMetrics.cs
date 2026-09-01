using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>
/// <see cref="IMessagingMetrics"/> that records nothing — so the messaging layer never forces a metrics dependency.
/// Register it before the transport (<c>services.AddSingleton&lt;IMessagingMetrics&gt;(NoOpMessagingMetrics.Instance)</c>)
/// and the default registration, which is <c>TryAdd</c>-based, stands down.
/// </summary>
public sealed class NoOpMessagingMetrics : IMessagingMetrics
{
    /// <summary>The shared instance — the type is stateless.</summary>
    public static readonly NoOpMessagingMetrics Instance = new();

    void IMessagingMetrics.RecordPublished(string destination, Type eventType) { }

    void IMessagingMetrics.RecordConsumed(string destination, Type eventType, ConsumeOutcome outcome) { }

    void IMessagingMetrics.RecordConsumeDuration(string destination, Type eventType, TimeSpan elapsed) { }

    void IMessagingMetrics.RecordDeadLettered(string destination, Type eventType, Exception? exception) { }

    void IMessagingMetrics.RecordRetried(string destination, Type eventType) { }

    IDisposable IMessagingMetrics.TrackInFlight(Func<int> probe) => NullRegistration.Instance;

    private sealed class NullRegistration : IDisposable
    {
        public static readonly NullRegistration Instance = new();

        public void Dispose() { }
    }
}
