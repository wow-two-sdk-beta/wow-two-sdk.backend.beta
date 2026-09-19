using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>Holds timing defaults for a <see cref="MessagingTestHarness"/>.</summary>
public sealed record MessagingHarnessOptions
{
    /// <summary>How long the bus must be silent before <see cref="MessagingTestHarness.WaitForIdleAsync"/> calls it idle. Default 100ms.</summary>
    /// <remarks>
    ///   - assumes a message handed to the transport reaches an observer within this window
    ///   - the in-memory transport needs microseconds
    ///   - raise it for a broker — too short reports idle while a message is still on the wire
    /// </remarks>
    public TimeSpan QuietPeriod { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Overall budget for a harness wait that does not pass its own. Default 5s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Re-check interval for the one condition that has no signal behind it — the in-flight count. Default 10ms.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(10);
}
