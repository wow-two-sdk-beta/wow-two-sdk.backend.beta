using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>The response contract for <see cref="PriceRequested"/>.</summary>
public sealed record PriceQuoted : IEvent
{
    /// <summary>Gets the quoted order's id.</summary>
    public required string OrderId { get; init; }

    /// <summary>Gets the quoted amount.</summary>
    public required decimal Amount { get; init; }
}
