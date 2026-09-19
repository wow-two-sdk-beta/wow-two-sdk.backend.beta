using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Holds reserved transport headers a scheduled saga timeout carries, so an instance can tell a live timeout from a cancelled or superseded one.</summary>
/// <remarks>
///   - both keys sit in the SDK's reserved <c>wt-</c> namespace
///   - propagation never copies a reserved key onto anything published while handling the timeout
/// </remarks>
public static class SagaHeaderConstants
{
    /// <summary>Holds the timeout's declared name — the key its token is stored under in <see cref="ISagaState.TimeoutTokens"/>.</summary>
    public const string TimeoutName = Transport.MessageHeaderConstants.ReservedPrefix + "saga-timeout-name";

    /// <summary>
    /// Holds the token minted when the timeout was scheduled. A timeout whose token no longer matches the one on the instance
    /// was cancelled or replaced while in flight, and is dropped — this is what makes "unschedule" work on a transport
    /// that cannot recall a message it already accepted.
    /// </summary>
    public const string TimeoutToken = Transport.MessageHeaderConstants.ReservedPrefix + "saga-timeout-token";
}
