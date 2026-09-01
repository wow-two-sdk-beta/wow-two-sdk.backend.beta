using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Per-call overrides for one <see cref="IRequestClient{TRequest, TResponse}"/> request.</summary>
public sealed record RequestOptions
{
    /// <summary>Response timeout for this call; null uses <see cref="RequestClientOptions.Timeout"/>.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Deliver the request point-to-point to this endpoint instead of publishing it by type. Null (default) publishes,
    /// so the request is routed to whichever endpoint binds the request type.
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>Correlation id for the wider business flow. The conversation id — which pairs this reply with this request — is always the client's own.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Id of the event that caused this request.</summary>
    public string? CausationId { get; set; }

    /// <summary>Partition / ordering key (transport-abstract).</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Persist the request so it survives a broker restart (transport-abstract).</summary>
    public bool? Durable { get; set; }

    /// <summary>Custom headers to attach to the request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}
