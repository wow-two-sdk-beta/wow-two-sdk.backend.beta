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

/// <summary>A contract given an explicit CloudEvents type token through <c>MapMessageType</c>.</summary>
public sealed record CheckoutCompleted(string OrderId, decimal Total) : IEvent;
