using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>A request contract answered by <see cref="PriceRequestedHandler"/>.</summary>
public sealed record PriceRequested : IEvent
{
    /// <summary>Gets the order id the price was requested for.</summary>
    public required string OrderId { get; init; }
}
