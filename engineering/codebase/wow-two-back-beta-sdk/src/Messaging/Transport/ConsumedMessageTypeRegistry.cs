using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Binds consumed message types to the process's registration-time topology. Populated from the registered
/// <see cref="IEventHandler{TEvent}"/> descriptors by
/// <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> and read once by
/// <see cref="TopologyService"/> to build bindings. Written during registration only — not thread-safe.
/// </summary>
public sealed class ConsumedMessageTypeRegistry
{
    private readonly HashSet<Type> _types = [];

    /// <summary>Record a consumed message type. Idempotent.</summary>
    /// <param name="messageType">The message contract type.</param>
    public void Add(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        _types.Add(messageType);
    }

    /// <summary>Every recorded type, ordered by full name so the declared topology is identical across restarts and instances.</summary>
    public IReadOnlyList<Type> Types => [.. _types.OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal)];
}
