using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>The result of running a single saga step.</summary>
public sealed record EventSagaStepOutcome
{
    private EventSagaStepOutcome(bool succeeded, string? failureReason)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
    }

    /// <summary>Whether the step succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>Failure reason when <see cref="Succeeded"/> is false.</summary>
    public string? FailureReason { get; }

    /// <summary>A successful outcome — the runner advances to the next step.</summary>
    public static EventSagaStepOutcome Completed() => new(true, null);

    /// <summary>A failed outcome — the runner compensates completed steps in reverse.</summary>
    /// <param name="reason">Why the step failed.</param>
    public static EventSagaStepOutcome Faulted(string reason) => new(false, reason);
}
