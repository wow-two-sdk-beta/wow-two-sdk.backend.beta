using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>Holds options for the in-memory event bus (the zero-broker default transport).</summary>
public sealed record InMemoryEventBusOptions
{
    /// <summary>Bounded channel capacity (backpressure when full). Must be positive. Default 1024.</summary>
    public int ChannelCapacity { get; set; } = 1024;

    /// <summary>Retry schedule applied to failed handler invocations before dead-lettering.</summary>
    public RetryConfig Retry { get; set; } = new();
}
