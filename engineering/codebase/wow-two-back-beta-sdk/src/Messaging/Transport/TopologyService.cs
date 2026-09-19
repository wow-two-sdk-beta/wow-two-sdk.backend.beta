using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Provides endpoint topology from consumed message types and stable routing tokens from
/// <see cref="IMessageTypeMapper"/>'s stable token. The token, not the assembly-qualified name: assembly identity
/// changes on a version bump or a type move, and a routing key built from it would silently stop matching the binding
/// the consumer declared.
/// </summary>
public sealed class TopologyService : ITopologyService
{
    /// <summary>Fallback shared-endpoint name when neither the options nor the transport supplied one.</summary>
    private const string FallbackSharedEndpointName = "wt.events.queue";

    /// <summary>AMQP encodes a routing key as a short string; anything longer is rejected at the frame level.</summary>
    private const int MaxRoutingKeyLength = 255;

    private readonly IMessageTypeMapper _typeResolver;
    private readonly Lazy<IReadOnlyList<EndpointTopology>> _endpoints;

    /// <summary>Create the provider.</summary>
    /// <param name="consumedTypes">The message types this process handles — the source of the bindings.</param>
    /// <param name="typeResolver">Supplies the stable wire token used as a routing key.</param>
    /// <param name="nameFormatter">Names endpoints and their dead-letter queues.</param>
    /// <param name="options">Topology shape options.</param>
    /// <param name="destinationBindings">Logical destination aliases this process answers to; null for none.</param>
    public TopologyService(
        ConsumedMessageTypeRegistry consumedTypes,
        IMessageTypeMapper typeResolver,
        IEndpointNameMapper nameFormatter,
        TopologyOptions options,
        DestinationBindingRegistry? destinationBindings = null)
    {
        ArgumentNullException.ThrowIfNull(consumedTypes);
        ArgumentNullException.ThrowIfNull(typeResolver);
        ArgumentNullException.ThrowIfNull(nameFormatter);
        ArgumentNullException.ThrowIfNull(options);

        _typeResolver = typeResolver;

        // Built on first use, when the consumed and alias sets are complete — a saga registered later still gets bindings.
        _endpoints = new Lazy<IReadOnlyList<EndpointTopology>>(() => Build(consumedTypes, nameFormatter, options, destinationBindings));
    }

    /// <inheritdoc />
    public IReadOnlyList<EndpointTopology> ConsumeEndpoints => _endpoints.Value;

    /// <inheritdoc />
    public string RoutingKeyFor(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return ToRoutingKey(_typeResolver.ToTypeToken(messageType));
    }

    /// <inheritdoc />
    public string ResolveRoutingKey(EventEnvelopeModel envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // An unset destination, or one equal to the type's own name, routes by type — any other value is an address.
        if (string.IsNullOrEmpty(envelope.Destination) || string.Equals(envelope.Destination, envelope.BodyType.Name, StringComparison.Ordinal))
            return RoutingKeyFor(envelope.BodyType);

        return ToRoutingKey(envelope.Destination);
    }

    private List<EndpointTopology> Build(
        ConsumedMessageTypeRegistry consumedTypes,
        IEndpointNameMapper nameFormatter,
        TopologyOptions options,
        DestinationBindingRegistry? destinationBindings)
    {
        var types = consumedTypes.Types;
        IReadOnlyList<DestinationBinding> aliases = destinationBindings?.Bindings ?? [];

        if (options.Style == TopologyStyle.EndpointPerMessageType)
        {
            var endpoints = new List<EndpointTopology>(types.Count);
            foreach (var type in types)
            {
                var queue = nameFormatter.Endpoint(type);
                endpoints.Add(new EndpointTopology
                {
                    Queue = queue,
                    DeadLetterQueue = nameFormatter.DeadLetter(queue),
                    RoutingKeys = BuildRoutingKeys(queue, [type], options, aliases),
                    MessageTypes = [type],
                });
            }

            return endpoints;
        }

        // Declared even with nothing to bind, so a service that temporarily handles nothing keeps its queues.
        var sharedQueue = options.SharedEndpointName ?? FallbackSharedEndpointName;
        return
        [
            new EndpointTopology
            {
                Queue = sharedQueue,
                DeadLetterQueue = options.SharedDeadLetterQueueName ?? nameFormatter.DeadLetter(sharedQueue),
                RoutingKeys = BuildRoutingKeys(sharedQueue, types, options, aliases),
                MessageTypes = types,
            },
        ];
    }

    private List<string> BuildRoutingKeys(string queue, IReadOnlyList<Type> types, TopologyOptions options, IReadOnlyList<DestinationBinding> aliases)
    {
        var keys = new List<string>((types.Count * 2) + 1);

        if (options.BindEndpointNameKeys)
            AddKey(keys, ToRoutingKey(queue));

        foreach (var type in types)
        {
            AddKey(keys, RoutingKeyFor(type));

            if (options.BindLegacyTypeNameKeys)
                AddKey(keys, ToRoutingKey(type.Name));
        }

        // An alias rides the endpoint carrying its type, and only where consumed — binding another service's diverts its traffic.
        if (options.BindDestinationAliasKeys)
            foreach (var alias in aliases)
                if (types.Contains(alias.MessageType))
                    AddKey(keys, ToRoutingKey(alias.Destination));

        return keys;

        static void AddKey(List<string> keys, string key)
        {
            if (key.Length != 0 && !keys.Contains(key, StringComparer.Ordinal))
                keys.Add(key);
        }
    }

    private static string ToRoutingKey(string token)
    {
        // Drops the assembly identity after the first comma — version and public key move on a rebuild.
        var assemblySeparator = token.IndexOf(',');
        var name = assemblySeparator >= 0 ? token[..assemblySeparator] : token;

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            // Keeps '.', the segment separator; everything else collapses to '-', so no wildcard reaches a key literally.
            var keep = char.IsLetterOrDigit(character) || character is '.' or '-' or '_';
            builder.Append(keep ? character : '-');
        }

        var key = builder.ToString().Trim('.');
        return key.Length <= MaxRoutingKeyLength ? key : key[..MaxRoutingKeyLength];
    }
}
