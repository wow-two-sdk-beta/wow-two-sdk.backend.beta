using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// The claim check itself — what travels on the wire in place of a body too large for the broker. Small, fixed-size,
/// and enough to fetch the real body back: where it is stored, how big it was, and how it was serialized.
/// </summary>
/// <remarks>Never publish through <see cref="IEventBus"/> — a wire artefact, not an <see cref="IEvent"/>, so no handler scan finds it.</remarks>
public sealed record ClaimCheckReference
{
    /// <summary>
    /// Stable wire token this type is registered under by <c>AddEventClaimCheck</c>, so a consumer resolves the wire
    /// body of an offloaded message without depending on the SDK's assembly-qualified name — whose version segment
    /// moves on every SDK push, and which would leave the message unresolvable across a version skew.
    /// </summary>
    public const string TypeToken = "wt.claim-check-reference";

    /// <summary>Logical blob path the body was written to, inside the configured <see cref="ClaimCheckOptions.PathPrefix"/>.</summary>
    public required string Path { get; init; }

    /// <summary>Size in bytes of the serialized body that was offloaded.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Content type the body was serialized with — checked on rehydrate so a serializer swap fails loudly rather than mis-decoding.</summary>
    public required string ContentType { get; init; }

    /// <summary>Wire token of the real body type; <see langword="null"/> when the wire already carries it as <c>wt-event-type</c>.</summary>
    public string? BodyType { get; init; }
}
