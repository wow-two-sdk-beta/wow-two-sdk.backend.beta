using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Default <see cref="IReplyAddressService"/> — the configured address, else this process's own consume endpoint from
/// <see cref="ITopologyService"/>, else a fixed fallback.
/// </summary>
/// <remarks>
///   - the topology already binds the consume endpoint under its own name (<see cref="TopologyOptions.BindEndpointNameKeys"/>), so a reply routes back with no extra declaration
///   - that address is shared — a reply can reach a second instance of the same service, which holds no pending entry, and the requester times out
///   - scale out only with a per-instance <see cref="RequestClientOptions.ReplyAddress"/> bound to that instance's own queue, or a replacement provider
/// </remarks>
public sealed class ReplyAddressService : IReplyAddressService
{
    /// <summary>Used when nothing is configured and no topology is registered — the transports in that position (Kafka, NATS) publish to their one configured topic/subject and ignore the address.</summary>
    private const string FallbackReplyAddress = "wt.responses";

    private readonly string _replyAddress;

    /// <summary>Create the provider.</summary>
    /// <param name="options">Request-client options; <see cref="RequestClientOptions.ReplyAddress"/> wins when set.</param>
    /// <param name="topology">The topology, when one is registered — its first consume endpoint is this process's address.</param>
    public ReplyAddressService(RequestClientOptions options, ITopologyService? topology = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.ReplyAddress;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _replyAddress = configured;
            return;
        }

        // Resolved once — the topology is fixed for the life of the process.
        var endpoints = topology?.ConsumeEndpoints;
        _replyAddress = endpoints is { Count: > 0 } ? endpoints[0].Queue : FallbackReplyAddress;
    }

    /// <inheritdoc />
    public string ReplyAddress => _replyAddress;
}
