namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Defines the idempotency seam for at-least-once delivery. Dedupes successful effects by message id and, for durable implementations, runs the
/// handler in the same transaction as the dedupe mark, so both commit or neither (closing the
/// mark-then-crash window). Returns <c>false</c> if already processed (skip).
/// </summary>
public interface IInboxProcessor
{
    /// <summary>Runs <paramref name="handler"/> once per successful <paramref name="messageId"/> commit; returns <c>false</c> if it was already processed.</summary>
    /// <param name="messageId">The message id (dedupe key).</param>
    /// <param name="handler">The processing action (dispatch); executed at most once per successful commit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> ProcessOnceAsync(string messageId, Func<CancellationToken, ValueTask> handler, CancellationToken cancellationToken);
}
