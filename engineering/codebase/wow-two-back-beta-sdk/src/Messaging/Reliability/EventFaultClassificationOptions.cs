using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Holds ordered classification rules, first match wins. Rules come as exception types
/// (<see cref="DeadLetterOn{TException}"/> and friends, which match subclasses too) and/or arbitrary predicates
/// (<see cref="Classify"/>). With no rules registered every exception retries — exactly the behaviour before
/// classification existed.
/// </summary>
public sealed record EventFaultClassificationOptions
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

    /// <summary>Retry <typeparamref name="TException"/> (and subclasses). Register it before a broader rule to carve one type back out of that rule.</summary>
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
