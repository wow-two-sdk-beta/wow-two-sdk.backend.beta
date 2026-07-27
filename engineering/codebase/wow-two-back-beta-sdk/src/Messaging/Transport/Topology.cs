using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>How consumed message types are mapped onto broker endpoints (queues).</summary>
public enum TopologyStyle
{
    /// <summary>
    /// One queue for the whole service, bound to one routing key per consumed message type. The queue name is the
    /// transport's configured one, so an existing deployment keeps its queue and its dead-letter queue.
    /// </summary>
    SharedEndpoint = 0,

    /// <summary>
    /// One queue per consumed message type, named by <see cref="IEndpointNameFormatter"/>, each with its own
    /// dead-letter queue. Gives per-type prefetch, isolation and DLQ inspection; needs new queues, so it is opt-in.
    /// </summary>
    EndpointPerMessageType = 1,
}

/// <summary>
/// Maps a message type to the endpoint (queue) that consumes it, and an endpoint to its dead-letter queue. Replace it
/// via <see cref="MessageTopologyServiceCollectionExtensions.AddEndpointNameFormatter{TFormatter}"/> to impose a house
/// naming scheme (per-team prefix, environment segment, an existing broker convention).
/// </summary>
public interface IEndpointNameFormatter
{
    /// <summary>The queue name of the endpoint that consumes <paramref name="messageType"/>.</summary>
    /// <param name="messageType">The message contract type.</param>
    string Endpoint(Type messageType);

    /// <summary>The dead-letter queue name for <paramref name="endpointName"/>.</summary>
    /// <param name="endpointName">An endpoint (queue) name, typically one returned by <see cref="Endpoint"/>.</param>
    string DeadLetter(string endpointName);
}

/// <summary>
/// Default formatter — kebab-cases the message type's simple name and joins it to an optional prefix with <c>.</c>:
/// <c>OrderPlaced</c> under prefix <c>wt.events</c> becomes <c>wt.events.order-placed</c>, dead-lettering to
/// <c>wt.events.order-placed.dlq</c>. Generic arguments are appended (<c>Envelope&lt;Order&gt;</c> → <c>envelope-order</c>).
/// The simple name is used, not the full name: queue names stay readable, at the cost of collapsing two same-named
/// types from different namespaces onto one endpoint. Register a replacement when that matters.
/// </summary>
public sealed class DefaultEndpointNameFormatter : IEndpointNameFormatter
{
    private readonly string? _prefix;
    private readonly string _deadLetterSuffix;

    /// <summary>Create the formatter.</summary>
    /// <param name="prefix">Dotted prefix put in front of every endpoint name (e.g. <c>wt.events</c>); null or empty for none.</param>
    /// <param name="deadLetterSuffix">Segment appended to an endpoint name to form its dead-letter queue. Default <c>dlq</c>.</param>
    public DefaultEndpointNameFormatter(string? prefix = null, string deadLetterSuffix = "dlq")
    {
        ArgumentException.ThrowIfNullOrEmpty(deadLetterSuffix);
        _prefix = prefix;
        _deadLetterSuffix = deadLetterSuffix;
    }

    /// <inheritdoc />
    public string Endpoint(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        var builder = new StringBuilder();
        AppendTypeName(builder, messageType);
        var name = builder.ToString();

        return string.IsNullOrEmpty(_prefix) ? name : string.Concat(_prefix, ".", name);
    }

    /// <inheritdoc />
    public string DeadLetter(string endpointName)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        return string.Concat(endpointName, ".", _deadLetterSuffix);
    }

    private static void AppendTypeName(StringBuilder builder, Type type)
    {
        var name = type.Name;

        // Arity marker on a generic type name (`Envelope`1`); the arguments are appended below in readable form.
        var arity = name.IndexOf('`');
        if (arity >= 0)
            name = name[..arity];

        AppendKebabCase(builder, name);

        if (!type.IsGenericType)
            return;

        foreach (var argument in type.GetGenericArguments())
        {
            AppendSeparator(builder);
            AppendTypeName(builder, argument);
        }
    }

    private static void AppendKebabCase(StringBuilder builder, string name)
    {
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (!char.IsLetterOrDigit(character))
            {
                AppendSeparator(builder);
                continue;
            }

            // A word starts at an upper-case letter that follows a lower-case one or a digit (orderPlaced →
            // order-placed), and at the last upper-case letter of an acronym run (HTTPRequest → http-request).
            var startsWord = char.IsUpper(character)
                && index > 0
                && (!char.IsUpper(name[index - 1]) || (index + 1 < name.Length && char.IsLower(name[index + 1])));

            if (startsWord)
                AppendSeparator(builder);

            builder.Append(char.ToLowerInvariant(character));
        }
    }

    private static void AppendSeparator(StringBuilder builder)
    {
        // Never leads, never doubles — a name is a sequence of words, not of separators.
        if (builder.Length > 0 && builder[^1] is not ('-' or '.'))
            builder.Append('-');
    }
}

/// <summary>One broker endpoint: the queue to declare and consume, its dead-letter queue, the routing keys bound to it, and the message types it carries.</summary>
public sealed record EndpointTopology
{
    /// <summary>The queue to declare and consume from.</summary>
    public required string Queue { get; init; }

    /// <summary>The dead-letter queue messages from <see cref="Queue"/> are moved to.</summary>
    public required string DeadLetterQueue { get; init; }

    /// <summary>
    /// Routing keys bound from the exchange to <see cref="Queue"/> — the stable type token of every type in
    /// <see cref="MessageTypes"/>, the queue's own name (so an explicit send addresses it point-to-point), and
    /// optionally the pre-topology short type name.
    /// </summary>
    public required IReadOnlyList<string> RoutingKeys { get; init; }

    /// <summary>The message types this endpoint consumes.</summary>
    public required IReadOnlyList<Type> MessageTypes { get; init; }
}

/// <summary>
/// Declares what a process consumes and how an outgoing message is addressed. This is what replaces a catch-all
/// binding: bindings are derived from the registered handler set, one routing key per message type, so a service
/// receives only the types it actually handles instead of filtering the whole exchange in-process.
/// </summary>
public interface ITopologyProvider
{
    /// <summary>Every endpoint this process declares, binds and consumes. Empty when the process handles nothing.</summary>
    IReadOnlyList<EndpointTopology> ConsumeEndpoints { get; }

    /// <summary>The routing key a message of <paramref name="messageType"/> is published under.</summary>
    /// <param name="messageType">The message contract type.</param>
    string RoutingKeyFor(Type messageType);

    /// <summary>The routing key for one outgoing envelope — the type key for a publish, the destination address for an explicit send.</summary>
    /// <param name="envelope">The envelope being sent.</param>
    string ResolveRoutingKey(EventEnvelope envelope);

    /// <summary>
    /// Whether <paramref name="routingKey"/> is bound to an endpoint of <em>this</em> process — a message addressed to
    /// it arrives here. The default answer walks <see cref="ConsumeEndpoints"/>; a provider that binds patterns rather
    /// than literal keys overrides it.
    /// </summary>
    /// <remarks>
    /// False is not proof the message is lost — another service may bind the key. It only says this process does not.
    /// Callers deciding whether a send will be dropped must pair it with knowledge of who owns the destination; see
    /// <see cref="DestinationBinding"/>, which records that ownership at registration time.
    /// </remarks>
    /// <param name="routingKey">A routing key, as returned by <see cref="RoutingKeyFor"/> or <see cref="ResolveRoutingKey"/>.</param>
    bool BindsRoutingKey(string routingKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(routingKey);

        foreach (var endpoint in ConsumeEndpoints)
            if (endpoint.RoutingKeys.Contains(routingKey, StringComparer.Ordinal))
                return true;

        return false;
    }
}

/// <summary>
/// Registration-time set of message types this process consumes. Populated from the registered
/// <see cref="IEventHandler{TEvent}"/> descriptors by
/// <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> and read once by
/// <see cref="DefaultTopologyProvider"/> to build bindings. Written during registration only — not thread-safe.
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

/// <summary>
/// A logical destination address this process answers to for one message type — an alias bound alongside the type
/// keys, so an explicit <see cref="IEventBus.SendAsync{TEvent}"/> to that address is delivered instead of dropped.
/// </summary>
/// <remarks>
/// A routing-slip saga step addresses its output by a logical name (<c>"order-fulfilment"</c>), not by type. Since
/// topology replaced the catch-all binding, only the endpoint queue names and the consumed types' keys are bound, so
/// such a name resolves to a key nothing binds and RabbitMQ discards the message without refusing it. Declaring the
/// pair here binds the alias, which is what makes the address deliverable.
/// <para>
/// The type is required and load-bearing: an alias is bound only where <see cref="MessageType"/> is actually
/// consumed. Binding an address blindly would attach another service's destination to this process's queue and steal
/// its messages — a louder failure than the drop, and a harder one to see.
/// </para>
/// </remarks>
public sealed record DestinationBinding
{
    /// <summary>The logical destination address, as passed to <see cref="IEventBus.SendAsync{TEvent}"/>.</summary>
    public required string Destination { get; init; }

    /// <summary>The message type sent to <see cref="Destination"/>. The alias is bound only on an endpoint carrying this type.</summary>
    public required Type MessageType { get; init; }
}

/// <summary>
/// Registration-time set of logical destination aliases this process answers to. Read by
/// <see cref="DefaultTopologyProvider"/> when it builds bindings, and by the routing-slip saga transport to tell a
/// destination it owns from one another service consumes. Written during registration only — not thread-safe.
/// </summary>
/// <remarks>
/// Populated through <see cref="MessageTopologyServiceCollectionExtensions.AddDestinationBinding"/>. The instance is
/// shared and endpoints are built lazily on first use, so registration order against
/// <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> does not matter.
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
    /// The type is part of the question on purpose: declaring an address for one type says nothing about a second type
    /// sent to the same address, and treating it as covered would hide exactly the drop this records.
    /// </remarks>
    /// <param name="destination">The logical destination address.</param>
    /// <param name="messageType">The message contract type sent to it.</param>
    public bool IsDeclared(string destination, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return !string.IsNullOrEmpty(destination) && _byDestination.TryGetValue(destination, out var types) && types.Contains(messageType);
    }
}

/// <summary>Topology shape — how endpoints are named and which routing keys each one binds.</summary>
public sealed class TopologyOptions
{
    /// <summary>Endpoint shape. Default <see cref="TopologyStyle.SharedEndpoint"/>, which keeps an existing deployment's queue names.</summary>
    public TopologyStyle Style { get; set; } = TopologyStyle.SharedEndpoint;

    /// <summary>Queue name for <see cref="TopologyStyle.SharedEndpoint"/>. Null lets the transport supply its own configured queue name.</summary>
    public string? SharedEndpointName { get; set; }

    /// <summary>Dead-letter queue for the shared endpoint. Null derives it from <see cref="IEndpointNameFormatter.DeadLetter"/>.</summary>
    public string? SharedDeadLetterQueueName { get; set; }

    /// <summary>Prefix handed to the default <see cref="IEndpointNameFormatter"/> for generated endpoint names (e.g. <c>wt.events</c>). Ignored when a custom formatter is registered.</summary>
    public string? EndpointPrefix { get; set; }

    /// <summary>
    /// Also bind the pre-topology routing key — the message type's simple name — alongside the stable type token.
    /// Default true: a publisher that has not been upgraded still emits the short name, and without this binding the
    /// exchange would drop those messages as unroutable. Turn it off once every publisher is on the new key.
    /// </summary>
    public bool BindLegacyTypeNameKeys { get; set; } = true;

    /// <summary>Bind each endpoint queue under its own name, so an explicit <see cref="IEventBus.SendAsync{TEvent}"/> to that name lands point-to-point. Default true.</summary>
    public bool BindEndpointNameKeys { get; set; } = true;

    /// <summary>
    /// Bind every declared <see cref="DestinationBinding"/> whose message type this process consumes, so a send to
    /// that logical address is delivered rather than dropped as unroutable. Default true. Turn it off only when an
    /// external topology already binds those aliases and a duplicate binding would double-deliver.
    /// </summary>
    public bool BindDestinationAliasKeys { get; set; } = true;
}

/// <summary>
/// Default <see cref="ITopologyProvider"/> — endpoints come from the consumed-type set, routing keys from
/// <see cref="IMessageTypeResolver"/>'s stable token. The token, not the assembly-qualified name: assembly identity
/// changes on a version bump or a type move, and a routing key built from it would silently stop matching the binding
/// the consumer declared.
/// </summary>
public sealed class DefaultTopologyProvider : ITopologyProvider
{
    /// <summary>Fallback shared-endpoint name when neither the options nor the transport supplied one.</summary>
    private const string FallbackSharedEndpointName = "wt.events.queue";

    /// <summary>AMQP encodes a routing key as a short string; anything longer is rejected at the frame level.</summary>
    private const int MaxRoutingKeyLength = 255;

    private readonly IMessageTypeResolver _typeResolver;
    private readonly Lazy<IReadOnlyList<EndpointTopology>> _endpoints;

    /// <summary>Create the provider.</summary>
    /// <param name="consumedTypes">The message types this process handles — the source of the bindings.</param>
    /// <param name="typeResolver">Supplies the stable wire token used as a routing key.</param>
    /// <param name="nameFormatter">Names endpoints and their dead-letter queues.</param>
    /// <param name="options">Topology shape options.</param>
    /// <param name="destinationBindings">Logical destination aliases this process answers to; null for none.</param>
    public DefaultTopologyProvider(
        ConsumedMessageTypeRegistry consumedTypes,
        IMessageTypeResolver typeResolver,
        IEndpointNameFormatter nameFormatter,
        IOptions<TopologyOptions> options,
        DestinationBindingRegistry? destinationBindings = null)
    {
        ArgumentNullException.ThrowIfNull(consumedTypes);
        ArgumentNullException.ThrowIfNull(typeResolver);
        ArgumentNullException.ThrowIfNull(nameFormatter);
        ArgumentNullException.ThrowIfNull(options);

        _typeResolver = typeResolver;

        // Built once, on first use rather than in the constructor: the consumed set is complete only after the whole
        // service collection has been built, and a transport resolves this provider well after that. The alias set is
        // read at the same moment, so a saga registered after AddMessageTopology still gets its bindings.
        _endpoints = new Lazy<IReadOnlyList<EndpointTopology>>(() => Build(consumedTypes, nameFormatter, options.Value, destinationBindings));
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
    public string ResolveRoutingKey(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // A publish stamps the body type's simple name as the destination; an explicit send stamps the caller's
        // address. So an unset destination, or one equal to the type's own name, means "route this by type" — and any
        // other value is an endpoint address to deliver to directly. This is the one place that decision lives: when
        // the envelope grows a real publish/send discriminator, only this method changes.
        if (string.IsNullOrEmpty(envelope.Destination) || string.Equals(envelope.Destination, envelope.BodyType.Name, StringComparison.Ordinal))
            return RoutingKeyFor(envelope.BodyType);

        return ToRoutingKey(envelope.Destination);
    }

    private List<EndpointTopology> Build(
        ConsumedMessageTypeRegistry consumedTypes,
        IEndpointNameFormatter nameFormatter,
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

        // The shared endpoint is declared even with nothing to bind: the queue and its dead-letter queue are part of
        // the deployment's contract, and a service that temporarily handles nothing should not delete them.
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

        // An alias rides the endpoint that carries its message type, in both styles — the shared queue when everything
        // shares one, that type's queue when they do not. Gated on the type being consumed here: an alias whose type
        // this process does not handle belongs to another service, and binding it locally would divert its traffic.
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
        // The resolver falls back to an assembly-qualified name for a type nothing registered. Everything from the
        // first comma is assembly identity — version and public key move on a rebuild — so only the namespace-
        // qualified name is kept, which is what a registered type would have produced anyway.
        var assemblySeparator = token.IndexOf(',');
        var name = assemblySeparator >= 0 ? token[..assemblySeparator] : token;

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            // '.' is the topic exchange's segment separator and is kept. '*' and '#' are its wildcards and must never
            // reach a key literally, or one type's binding would swallow another's. '+' (nested type) and '`'
            // (generic arity) are not addressable characters, so every other character collapses to '-'.
            var keep = char.IsLetterOrDigit(character) || character is '.' or '-' or '_';
            builder.Append(keep ? character : '-');
        }

        var key = builder.ToString().Trim('.');
        return key.Length <= MaxRoutingKeyLength ? key : key[..MaxRoutingKeyLength];
    }
}

/// <summary>DI registration for message topology — endpoint naming, the consumed-type set, and the topology provider.</summary>
public static class MessageTopologyServiceCollectionExtensions
{
    /// <summary>
    /// Register the default topology — <see cref="ITopologyProvider"/>, <see cref="IEndpointNameFormatter"/>,
    /// <see cref="TopologyOptions"/> — and record every message type this process consumes.
    /// <para>
    /// Call it <em>after</em> handlers are registered (<c>AddEventHandlersFromAssemblies</c>): the consumed set is read
    /// from the registered <see cref="IEventHandler{TEvent}"/> descriptors, so a handler registered afterwards gets no
    /// binding and its messages never arrive. Repeat calls are additive — the recorded set is shared.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional topology options (style, endpoint prefix, legacy bindings).</param>
    public static IServiceCollection AddMessageTopology(this IServiceCollection services, Action<TopologyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<TopologyOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        // Registered unconditionally so DefaultTopologyProvider always resolves the same instance, whichever of
        // AddMessageTopology / AddDestinationBinding the application calls first.
        GetOrAddDestinationBindings(services);

        var consumedTypes = GetOrAddConsumedTypes(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType is not { IsGenericType: true, ContainsGenericParameters: false } serviceType)
                continue;

            if (serviceType.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                consumedTypes.Add(serviceType.GetGenericArguments()[0]);
        }

        services.TryAddSingleton<IEndpointNameFormatter>(static provider =>
            new DefaultEndpointNameFormatter(provider.GetRequiredService<IOptions<TopologyOptions>>().Value.EndpointPrefix));
        services.TryAddSingleton<ITopologyProvider, DefaultTopologyProvider>();
        return services;
    }

    /// <summary>Replace the endpoint name formatter with a house naming scheme.</summary>
    /// <typeparam name="TFormatter">The formatter implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEndpointNameFormatter<TFormatter>(this IServiceCollection services)
        where TFormatter : class, IEndpointNameFormatter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IEndpointNameFormatter, TFormatter>());
        return services;
    }

    /// <summary>
    /// Declare that this process consumes <paramref name="messageType"/> addressed to the logical destination
    /// <paramref name="destination"/>, binding that address alongside the type's own routing key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Needed by anything that addresses a message by a logical name rather than by type — a routing-slip saga step
    /// above all. Since topology replaced the catch-all binding, an address nothing bound resolves to a key nothing
    /// matches, and RabbitMQ accepts and discards the message: <c>mandatory: false</c> means unroutable is not an
    /// error. Declaring the address is what makes it deliverable.
    /// </para>
    /// <para>
    /// The alias binds only where <paramref name="messageType"/> is consumed, so declaring a destination another
    /// service owns is safe and records who owns it — the saga transport uses that to tell a legitimate cross-service
    /// address from a misspelt local one. Order against <see cref="AddMessageTopology"/> does not matter.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="destination">The logical destination address, exactly as passed to <see cref="IEventBus.SendAsync{TEvent}"/>.</param>
    /// <param name="messageType">The message contract type sent to that address.</param>
    public static IServiceCollection AddDestinationBinding(this IServiceCollection services, string destination, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(messageType);

        GetOrAddDestinationBindings(services).Add(destination, messageType);
        return services;
    }

    /// <summary>Declare a logical destination this process consumes <typeparamref name="TEvent"/> on.</summary>
    /// <typeparam name="TEvent">The message contract type sent to that address.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="destination">The logical destination address.</param>
    public static IServiceCollection AddDestinationBinding<TEvent>(this IServiceCollection services, string destination)
        where TEvent : class, IEvent
        => services.AddDestinationBinding(destination, typeof(TEvent));

    /// <summary>Replace the topology provider — for content/header routing, a schema registry, or an existing broker layout.</summary>
    /// <typeparam name="TProvider">The provider implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddTopologyProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITopologyProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<ITopologyProvider, TProvider>());
        return services;
    }

    private static ConsumedMessageTypeRegistry GetOrAddConsumedTypes(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(ConsumedMessageTypeRegistry) && descriptor.ImplementationInstance is ConsumedMessageTypeRegistry existing)
                return existing;

        var registry = new ConsumedMessageTypeRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static DestinationBindingRegistry GetOrAddDestinationBindings(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(DestinationBindingRegistry) && descriptor.ImplementationInstance is DestinationBindingRegistry existing)
                return existing;

        var registry = new DestinationBindingRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
