using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// Handles <see cref="HarnessEvent"/> through the optional <see cref="HarnessGate"/>.
/// </summary>
/// <remarks>
/// The gate is pulled from the provider rather than injected: this handler is scanned into every host in the assembly,
/// including the broker suites that never register a gate, and a constructor dependency they cannot satisfy would trip
/// container validation there.
/// </remarks>
public sealed class HarnessHandler(IServiceProvider services) : IEventHandler<HarnessEvent>
{
    public ValueTask HandleAsync(EventContext<HarnessEvent> context, CancellationToken cancellationToken)
    {
        var gate = services.GetService<HarnessGate>();
        return gate is null ? ValueTask.CompletedTask : new ValueTask(gate.RunAsync(cancellationToken));
    }
}
