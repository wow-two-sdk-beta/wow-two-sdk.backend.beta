using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Well-known saga state names. Every other state is an application-defined string.</summary>
/// <remarks>
///   - the current state persists verbatim into <see cref="ISagaState.CurrentState"/>, surviving a rename of the C# member
///   - a typo names a state nothing transitions from, so the event is ignored with no compile error
///   - declare states as <c>const</c> on the state machine
/// </remarks>
public static class SagaStateConstants
{
    /// <summary>The state an instance is in before its first transition — what <c>Initially</c> binds to.</summary>
    public const string Initial = "initial";

    /// <summary>The terminal state. Reaching it finalizes the instance: removed, or retained per <see cref="SagaOptions.RemoveOnFinalize"/>.</summary>
    public const string Final = "final";

    /// <summary>Wildcard source state used by <c>DuringAny</c> — matches an instance in any state. Do not name a real state this.</summary>
    public const string Any = "*";
}
