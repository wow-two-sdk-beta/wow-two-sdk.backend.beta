using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Convenience base for a saga state — implements every <see cref="ISagaState"/> member, so an application state adds
/// only its own business fields.
/// </summary>
/// <remarks>
///   - <see cref="Copy"/> is shallow plus a fresh timeout dictionary — a nested mutable object stays shared
///   - keep saga state flat, scalars and ids only
///   - override <see cref="Copy"/> where a nested object is unavoidable
/// </remarks>
public abstract class SagaState : ISagaState
{
    private Dictionary<string, string> _timeoutTokens = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string CorrelationId { get; set; } = string.Empty;

    /// <inheritdoc />
    public string CurrentState { get; set; } = SagaStateConstants.Initial;

    /// <inheritdoc />
    public int Version { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? FinalizedAtUtc { get; set; }

    /// <inheritdoc />
    public IDictionary<string, string> TimeoutTokens => _timeoutTokens;

    /// <inheritdoc />
    public virtual ISagaState Copy()
    {
        var copy = (SagaState)MemberwiseClone();

        // Deep-copy the token map — MemberwiseClone leaves the clone sharing the original's dictionary.
        copy._timeoutTokens = new Dictionary<string, string>(_timeoutTokens, StringComparer.Ordinal);
        return copy;
    }
}
