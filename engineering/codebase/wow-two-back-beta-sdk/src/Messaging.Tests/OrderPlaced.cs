using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Initiating event — correlates by order id.</summary>
public sealed record OrderPlaced : IEvent
{
    /// <summary>Gets the placed order's id.</summary>
    public required string OrderId { get; init; }

    /// <summary>Gets the placed order's total.</summary>
    public required decimal Total { get; init; }
}
