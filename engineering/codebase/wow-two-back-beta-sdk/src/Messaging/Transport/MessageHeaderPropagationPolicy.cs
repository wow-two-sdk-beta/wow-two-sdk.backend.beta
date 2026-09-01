namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Allow-list <see cref="IMessageHeaderPropagationPolicy"/> — a header flows only if its name is on the list.
/// Immutable; <see cref="Allow"/> returns a widened copy rather than mutating the instance.
/// </summary>
/// <remarks>
///   - names match case-insensitively, per HTTP/W3C — <c>Tenant-Id</c> matches <c>tenant-id</c>
///   - reserved keys never flow — <see cref="ShouldPropagate"/> rejects them before consulting the list
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
    /// The default policy: W3C trace context only (<see cref="MessageHeaderConstants.TraceParent"/> +
    /// <see cref="MessageHeaderConstants.TraceState"/>), matching what the SDK propagated before the policy seam existed.
    /// </summary>
    public static MessageHeaderPropagationPolicy Default { get; } = new(MessageHeaderConstants.TraceParent, MessageHeaderConstants.TraceState);

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
    public bool ShouldPropagate(string key) => !string.IsNullOrEmpty(key) && !MessageHeaderConstants.IsReserved(key) && _allowed.Contains(key);
}
