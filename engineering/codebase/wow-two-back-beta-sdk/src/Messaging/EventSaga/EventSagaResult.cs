using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>The outcome of a whole event-saga run.</summary>
/// <param name="Succeeded">Whether every step completed.</param>
/// <param name="FailedStep">Name of the step that failed, if any.</param>
/// <param name="FailureReason">Why the saga failed, if it did.</param>
/// <param name="CompensatedSteps">Names of steps compensated (reverse order), if the saga rolled back.</param>
public sealed record EventSagaResult(
    bool Succeeded,
    string? FailedStep,
    string? FailureReason,
    IReadOnlyList<string> CompensatedSteps);
