using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Registration-time set of logical destination aliases this process answers to. Read by
/// <see cref="TopologyService"/> when it builds bindings, and by the routing-slip saga transport to tell a
/// destination it owns from one another service consumes. Written during registration only — not thread-safe.
/// </summary>
/// <remarks>
///   - populate through <see cref="MessageTopologyServiceCollectionExtensions.AddDestinationBinding"/>
///   - order against <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> does not matter
/// </remarks>
public sealed class DestinationBindingRegistry
{
    private readonly Dictionary<string, HashSet<Type>> _byDestination = new(StringComparer.Ordinal);

    /// <summary>Record that <paramref name="destination"/> carries <paramref name="messageType"/>. Idempotent.</summary>
    /// <param name="destination">The logical destination address.</param>
    /// <param name="messageType">The message contract type sent to it.</param>
    public void Add(string destination, Type messageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(messageType);

        if (!_byDestination.TryGetValue(destination, out var types))
            _byDestination[destination] = types = [];

        types.Add(messageType);
    }

    /// <summary>Every declared pair, ordered so the declared topology is identical across restarts and instances.</summary>
    public IReadOnlyList<DestinationBinding> Bindings =>
    [
        .. _byDestination
            .SelectMany(static pair => pair.Value.Select(type => new DestinationBinding { Destination = pair.Key, MessageType = type }))
            .OrderBy(static binding => binding.Destination, StringComparer.Ordinal)
            .ThenBy(static binding => binding.MessageType.FullName ?? binding.MessageType.Name, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Whether a registration declared <paramref name="messageType"/> on <paramref name="destination"/> — i.e. this
    /// process knows who consumes that pair, whether locally or in another service.
    /// </summary>
    /// <remarks>
    ///   - declaring an address for one type says nothing about another type sent to it
    ///   - ask per type, never per address
    /// </remarks>
    /// <param name="destination">The logical destination address.</param>
    /// <param name="messageType">The message contract type sent to it.</param>
    public bool IsDeclared(string destination, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return !string.IsNullOrEmpty(destination) && _byDestination.TryGetValue(destination, out var types) && types.Contains(messageType);
    }
}
