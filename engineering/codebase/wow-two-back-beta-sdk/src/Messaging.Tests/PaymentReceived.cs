using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Advancing event — correlates by the same order id.</summary>
public sealed record PaymentReceived : IEvent
{
    /// <summary>Gets the paid order's id.</summary>
    public required string OrderId { get; init; }

    /// <summary>Gets the payment amount received.</summary>
    public required decimal Amount { get; init; }
}
