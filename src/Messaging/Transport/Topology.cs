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
    public DefaultTopologyProvider(
        ConsumedMessageTypeRegistry consumedTypes,
        IMessageTypeResolver typeResolver,
        IEndpointNameFormatter nameFormatter,
        IOptions<TopologyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(consumedTypes);
        ArgumentNullException.ThrowIfNull(typeResolver);
        ArgumentNullException.ThrowIfNull(nameFormatter);
        ArgumentNullException.ThrowIfNull(options);

        _typeResolver = typeResolver;

        // Built once, on first use rather than in the constructor: the consumed set is complete only after the whole
        // service collection has been built, and a transport resolves this provider well after that.
        _endpoints = new Lazy<IReadOnlyList<EndpointTopology>>(() => Build(consumedTypes, nameFormatter, options.Value));
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

    private List<EndpointTopology> Build(ConsumedMessageTypeRegistry consumedTypes, IEndpointNameFormatter nameFormatter, TopologyOptions options)
    {
        var types = consumedTypes.Types;

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
                    RoutingKeys = BuildRoutingKeys(queue, [type], options),
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
                RoutingKeys = BuildRoutingKeys(sharedQueue, types, options),
                MessageTypes = types,
            },
        ];
    }

    private List<string> BuildRoutingKeys(string queue, IReadOnlyList<Type> types, TopologyOptions options)
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
}
