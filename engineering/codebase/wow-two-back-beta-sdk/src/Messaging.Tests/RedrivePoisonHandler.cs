using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Handles test redrives by always throwing.</summary>
public sealed class RedrivePoisonHandler : IEventHandler<RedrivePoison>
{
    public ValueTask HandleAsync(EventContext<RedrivePoison> context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("poison");
}
