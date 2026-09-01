namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Applies an <see cref="IMessageHeaderPropagationPolicy"/> to build the header set of an outgoing message.</summary>
public static class MessageHeaderExtensions
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
                // Reserved keys never carry forward — a control header describes the message it arrived on.
                if (MessageHeaderConstants.IsReserved(key) || !policy.ShouldPropagate(key))
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
