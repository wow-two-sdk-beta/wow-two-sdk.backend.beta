using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// Handles test events by throwing while the toggle says to. The toggle is pulled from the provider rather than injected, because this handler is
/// scanned into every host in the assembly and most of them never register one.
/// </summary>
public sealed class FlakyHandler(IServiceProvider services) : IEventHandler<FlakyEvent>
{
    public ValueTask HandleAsync(EventContext<FlakyEvent> context, CancellationToken cancellationToken)
    {
        if (services.GetService<FlakyToggle>() is { Fail: true })
            throw new TimeoutException("flaky downstream");

        return ValueTask.CompletedTask;
    }
}
