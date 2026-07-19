using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>What an <see cref="IEventResiliencePipeline"/> does with an exception thrown by a consume attempt.</summary>
public enum FaultDisposition
{
    /// <summary>Retry under the configured schedule, then propagate once attempts are exhausted. The default for every exception.</summary>
    Retry,

    /// <summary>
    /// Propagate immediately without spending an attempt — nothing about a redelivery would change the outcome
    /// (validation, deserialization, a missing contract), so the consume pipeline dead-letters it on the first failure.
    /// </summary>
    DeadLetter,

    /// <summary>
    /// Swallow: the resilience pipeline returns normally, so the consume pipeline takes its success path and
    /// acknowledges the message. For failures that are expected and uninteresting (already-applied, stale-version).
    /// </summary>
    Ignore,
}

/// <summary>
/// Sorts a thrown exception into a <see cref="FaultDisposition"/> so the resilience pipeline can skip the retry budget
/// for failures that cannot succeed on redelivery. Every <see cref="IEventResiliencePipeline"/> implementation consults
/// the same registered classifier, so behaviour does not depend on which pipeline is registered.
/// </summary>
/// <remarks>
/// Implementations MUST be thread-safe and SHOULD NOT throw — <see cref="Classify"/> runs on the consume path for every
/// failed attempt, and one pipeline evaluates it inside an exception filter.
/// </remarks>
public interface IEventFaultClassifier
{
    /// <summary>Classify <paramref name="exception"/>; return <see cref="FaultDisposition.Retry"/> when there is no reason to treat it specially.</summary>
    /// <param name="exception">The exception thrown by the attempt.</param>
    FaultDisposition Classify(Exception exception);
}

/// <summary>
/// Ordered classification rules, <b>first match wins</b>. Rules come as exception types
/// (<see cref="DeadLetterOn{TException}"/> and friends, which match subclasses too) and/or arbitrary predicates
/// (<see cref="Classify"/>). With no rules registered every exception retries — exactly the behaviour before
/// classification existed.
/// </summary>
public sealed class EventFaultClassificationOptions
{
    private readonly List<Func<Exception, FaultDisposition?>> _rules = [];

    internal IReadOnlyList<Func<Exception, FaultDisposition?>> Rules => _rules;

    /// <summary>Fail fast on <typeparamref name="TException"/> (and subclasses) — propagate without retrying, so the message is dead-lettered on its first failure.</summary>
    /// <typeparam name="TException">The exception type to reject.</typeparam>
    public EventFaultClassificationOptions DeadLetterOn<TException>()
        where TException : Exception
        => OnType<TException>(FaultDisposition.DeadLetter);

    /// <summary>Treat <typeparamref name="TException"/> (and subclasses) as handled — swallow it, so the message is acknowledged.</summary>
    /// <typeparam name="TException">The exception type to ignore.</typeparam>
    public EventFaultClassificationOptions IgnoreOn<TException>()
        where TException : Exception
        => OnType<TException>(FaultDisposition.Ignore);

    /// <summary>Retry <typeparamref name="TException"/> (and subclasses). Register it <b>before</b> a broader rule to carve one type back out of that rule.</summary>
    /// <typeparam name="TException">The exception type to keep retrying.</typeparam>
    public EventFaultClassificationOptions RetryOn<TException>()
        where TException : Exception
        => OnType<TException>(FaultDisposition.Retry);

    /// <summary>Add a predicate rule — for classification that depends on the exception's state (an HTTP status, an error code) rather than its type.</summary>
    /// <param name="rule">Returns a disposition to decide, or <c>null</c> to defer to the following rules. A rule that throws is treated as <c>null</c>.</param>
    public EventFaultClassificationOptions Classify(Func<Exception, FaultDisposition?> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }

    private EventFaultClassificationOptions OnType<TException>(FaultDisposition disposition)
        where TException : Exception
        => Classify(exception => exception is TException ? disposition : null);
}

/// <summary>
/// Default <see cref="IEventFaultClassifier"/> — walks the configured rules in registration order, takes the first
/// non-null verdict, and falls back to <see cref="FaultDisposition.Retry"/>.
/// </summary>
public sealed class DefaultEventFaultClassifier : IEventFaultClassifier
{
    private readonly Func<Exception, FaultDisposition?>[] _rules;

    /// <summary>Build a classifier over the configured rules.</summary>
    /// <param name="options">The classification rules.</param>
    public DefaultEventFaultClassifier(EventFaultClassificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _rules = [.. options.Rules];
    }

    /// <summary>The rule-free classifier — every exception retries. Registered by default, and the fallback when nothing is registered.</summary>
    public static DefaultEventFaultClassifier RetryAll { get; } = new(new EventFaultClassificationOptions());

    /// <inheritdoc />
    public FaultDisposition Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        foreach (var rule in _rules)
        {
            FaultDisposition? verdict;
            try
            {
                verdict = rule(exception);
            }
            catch (Exception)
            {
                continue; // a failing rule must not decide a message's fate — fall through to the next one
            }

            if (verdict is { } disposition)
                return disposition;
        }

        return FaultDisposition.Retry;
    }
}

/// <summary>DI registration for consume-side exception classification.</summary>
public static class EventFaultClassificationServiceCollectionExtensions
{
    /// <summary>
    /// Configure which exceptions skip the retry budget. Applies to <b>every</b> <see cref="IEventResiliencePipeline"/>
    /// implementation (default and Polly-backed), so classification never depends on which one is registered.
    /// Call order relative to the transport/resilience registrations does not matter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The classification rules, evaluated first-match-wins.</param>
    /// <example>
    /// <code>
    /// services.AddEventFaultClassification(rules => rules
    ///     .DeadLetterOn&lt;ValidationException&gt;()   // never succeeds on redelivery → dead-letter now
    ///     .DeadLetterOn&lt;JsonException&gt;()
    ///     .IgnoreOn&lt;AlreadyAppliedException&gt;()); // expected → acknowledge and move on
    /// </code>
    /// </example>
    public static IServiceCollection AddEventFaultClassification(this IServiceCollection services, Action<EventFaultClassificationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new EventFaultClassificationOptions();
        configure(options);
        services.Replace(ServiceDescriptor.Singleton<IEventFaultClassifier>(new DefaultEventFaultClassifier(options)));
        return services;
    }

    /// <summary>Replace the rule-based classifier with a custom <see cref="IEventFaultClassifier"/> implementation.</summary>
    /// <typeparam name="TClassifier">The classifier implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEventFaultClassifier<TClassifier>(this IServiceCollection services)
        where TClassifier : class, IEventFaultClassifier
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IEventFaultClassifier, TClassifier>());
        return services;
    }
}
