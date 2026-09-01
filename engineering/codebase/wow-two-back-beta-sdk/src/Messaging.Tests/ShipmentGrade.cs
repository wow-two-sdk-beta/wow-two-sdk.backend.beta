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

/// <summary>Grade of a shipment — an enum member of <see cref="SerializerPayload"/>, so every serializer's enum policy is exercised by the round trip.</summary>
public enum ShipmentGrade
{
    Standard = 0,
    Express = 1,
    Overnight = 2,
}
