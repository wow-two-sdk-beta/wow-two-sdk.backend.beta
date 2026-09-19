using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>An event whose handler holds a slot long enough for the pump's concurrency to be observable.</summary>
public sealed record SlowEvent : IEvent
{
    /// <summary>Gets the event's tag.</summary>
    public required string Tag { get; init; }
}
