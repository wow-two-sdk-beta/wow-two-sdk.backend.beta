namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Decides which headers flow from a consumed message onto a message published while handling it. The seam exists
/// because "carry the tenant id forward, drop the caller's debug flag" is an application decision, not a transport one.
/// </summary>
/// <remarks>
/// Applied by <see cref="EventContext{TEvent}"/>, the only place that holds both the inbound envelope and the outgoing
/// options. A message published straight from <see cref="IEventBus"/> has no inbound message and so nothing to
/// propagate. Replace the registered policy to change what flows; the default is
/// <see cref="MessageHeaderPropagationPolicy.Default"/>.
/// </remarks>
public interface IMessageHeaderPropagationPolicy
{
    /// <summary>
    /// True when a header of this name should be copied from the consumed message onto the outgoing one. Reserved
    /// (<see cref="MessageHeaders.ReservedPrefix"/>) keys are blocked by the caller regardless of the answer, so an
    /// implementation never has to guard the control namespace itself.
    /// </summary>
    /// <param name="key">The inbound header key.</param>
    bool ShouldPropagate(string key);
}

/// <summary>
/// Allow-list <see cref="IMessageHeaderPropagationPolicy"/> — a header flows only if its name is on the list.
/// Immutable; <see cref="Allow"/> returns a widened copy rather than mutating the instance.
/// </summary>
/// <remarks>
/// Names match case-insensitively, following HTTP/W3C header semantics — an allow-list entry of <c>Tenant-Id</c>
/// matches a wire header of <c>tenant-id</c>. Reserved keys can never be allowed: <see cref="ShouldPropagate"/> rejects
/// them before consulting the list, so the SDK's control headers cannot be re-stamped onto a different body.
/// </remarks>
public sealed class MessageHeaderPropagationPolicy : IMessageHeaderPropagationPolicy
{
    private readonly HashSet<string> _allowed;

    /// <summary>Create a policy allowing exactly the given header names.</summary>
    /// <param name="headerNames">Header names that may flow from a consumed message to one published while handling it.</param>
    public MessageHeaderPropagationPolicy(params string[] headerNames)
        : this((IEnumerable<string>)headerNames)
    {
    }

    /// <summary>Create a policy allowing exactly the given header names.</summary>
    /// <param name="headerNames">Header names that may flow from a consumed message to one published while handling it.</param>
    public MessageHeaderPropagationPolicy(IEnumerable<string> headerNames)
    {
        ArgumentNullException.ThrowIfNull(headerNames);
        _allowed = new HashSet<string>(headerNames, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The default policy: W3C trace context only (<see cref="MessageHeaders.TraceParent"/> +
    /// <see cref="MessageHeaders.TraceState"/>), matching what the SDK propagated before the policy seam existed.
    /// </summary>
    /// <remarks>
    /// On a traced path the copy is inert — the producer <see cref="System.Diagnostics.Activity"/> re-stamps both keys
    /// with the fresh child context on the way to the transport, overwriting whatever was carried across. It carries
    /// only when nothing is listening to the <c>ActivitySource</c>, keeping the outgoing message inside the inbound
    /// trace instead of dropping it.
    /// </remarks>
    public static MessageHeaderPropagationPolicy Default { get; } = new(MessageHeaders.TraceParent, MessageHeaders.TraceState);

    /// <summary>A policy that propagates nothing — every outgoing message starts with a clean header set.</summary>
    public static MessageHeaderPropagationPolicy None { get; } = new();

    /// <summary>The allowed header names.</summary>
    public IReadOnlyCollection<string> AllowedHeaders => _allowed;

    /// <summary>Return a copy of this policy widened by the given header names.</summary>
    /// <param name="headerNames">Additional header names to allow.</param>
    public MessageHeaderPropagationPolicy Allow(params string[] headerNames)
    {
        ArgumentNullException.ThrowIfNull(headerNames);
        return new MessageHeaderPropagationPolicy(_allowed.Concat(headerNames));
    }

    /// <inheritdoc />
    public bool ShouldPropagate(string key) => !string.IsNullOrEmpty(key) && !MessageHeaders.IsReserved(key) && _allowed.Contains(key);
}

/// <summary>Applies an <see cref="IMessageHeaderPropagationPolicy"/> to build the header set of an outgoing message.</summary>
public static class MessageHeaderPropagation
{
    /// <summary>
    /// Build the headers for a message published while handling <paramref name="inbound"/>: the inbound headers the
    /// policy allows, overlaid with <paramref name="explicitHeaders"/> the caller set on the publish/send options.
    /// Explicit headers win — the caller asking for a value outranks one carried across — and bypass the policy, which
    /// governs propagation only.
    /// </summary>
    /// <param name="policy">The propagation policy.</param>
    /// <param name="inbound">Headers of the consumed message.</param>
    /// <param name="explicitHeaders">Headers set explicitly on the outgoing message; may be null.</param>
    /// <returns>The merged headers, or null when the result would be empty — so an unconfigured path stays allocation-free.</returns>
    public static IReadOnlyDictionary<string, string>? BuildOutboundHeaders(
        this IMessageHeaderPropagationPolicy policy,
        IReadOnlyDictionary<string, string>? inbound,
        IReadOnlyDictionary<string, string>? explicitHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        Dictionary<string, string>? outbound = null;

        if (inbound is { Count: > 0 })
        {
            foreach (var (key, value) in inbound)
            {
                // Reserved keys are blocked here, not in the policy: a control header describes the message it arrived
                // on, so carrying one forward would stamp the previous body's type token or message id onto a new body.
                if (MessageHeaders.IsReserved(key) || !policy.ShouldPropagate(key))
                    continue;

                (outbound ??= new Dictionary<string, string>(StringComparer.Ordinal))[key] = value;
            }
        }

        if (explicitHeaders is { Count: > 0 })
        {
            foreach (var (key, value) in explicitHeaders)
                (outbound ??= new Dictionary<string, string>(StringComparer.Ordinal))[key] = value;
        }

        return outbound;
    }
}
