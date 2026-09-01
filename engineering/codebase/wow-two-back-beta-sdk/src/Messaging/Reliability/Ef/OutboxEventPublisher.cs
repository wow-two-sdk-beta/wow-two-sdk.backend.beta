using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>Publishes a runtime-typed event to the <see cref="IEventBus"/> via a cached compiled delegate (bridges the generic <c>PublishAsync&lt;TEvent&gt;</c>).</summary>
internal sealed class OutboxEventPublisher(IEventBus bus)
{
    private static readonly ConcurrentDictionary<Type, Func<IEventBus, object, PublishOptions?, CancellationToken, ValueTask>> Invokers = new();

    public ValueTask PublishAsync(Type eventType, object @event, PublishOptions? options, CancellationToken cancellationToken)
        => Invokers.GetOrAdd(eventType, BuildInvoker)(bus, @event, options, cancellationToken);

    private static Func<IEventBus, object, PublishOptions?, CancellationToken, ValueTask> BuildInvoker(Type eventType)
    {
        var method = typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))!.MakeGenericMethod(eventType);
        var busParam = Expression.Parameter(typeof(IEventBus), "bus");
        var eventParam = Expression.Parameter(typeof(object), "event");
        // Options flow through as a parameter, so one compiled invoker per event type serves every row.
        var optionsParam = Expression.Parameter(typeof(PublishOptions), "options");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var call = Expression.Call(
            busParam,
            method,
            Expression.Convert(eventParam, eventType),
            optionsParam,
            ctParam);
        return Expression.Lambda<Func<IEventBus, object, PublishOptions?, CancellationToken, ValueTask>>(call, busParam, eventParam, optionsParam, ctParam).Compile();
    }
}
