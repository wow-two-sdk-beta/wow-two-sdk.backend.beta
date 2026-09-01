using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

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
