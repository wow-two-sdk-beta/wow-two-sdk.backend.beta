using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>Represents the outcome of a whole event-saga run.</summary>
public sealed record EventSagaResult
{
    /// <summary>Whether every step completed.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Name of the step that failed, if any.</summary>
    public required string? FailedStep { get; init; }

    /// <summary>Why the saga failed, if it did.</summary>
    public required string? FailureReason { get; init; }

    /// <summary>Names of steps compensated (reverse order), if the saga rolled back.</summary>
    public required IReadOnlyList<string> CompensatedSteps { get; init; }
}
