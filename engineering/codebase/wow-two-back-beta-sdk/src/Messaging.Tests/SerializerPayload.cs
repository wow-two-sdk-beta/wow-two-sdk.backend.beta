using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// The shared round-trip contract: a record (positional, so there is a constructor to bind), a collection, a nullable
/// left null, an enum, and a <see cref="DateTimeOffset"/> carrying a non-UTC offset.
/// </summary>
/// <remarks>
/// Deliberately carries no <c>[MessagePackObject]</c>/<c>[Key]</c> annotation — the contractless resolver has to bind it
/// as-is, which is the promise that lets a contract survive the swap away from System.Text.Json.
/// </remarks>
public sealed record SerializerPayload
{
    /// <summary>Gets the payload name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the payload count.</summary>
    public required int Count { get; init; }

    /// <summary>Gets the shipment grade.</summary>
    public required ShipmentGrade Grade { get; init; }

    /// <summary>Gets when the payload occurred.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Gets the payload's tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Gets the optional value, left null in the round-trip contract.</summary>
    public int? Optional { get; init; }
}
