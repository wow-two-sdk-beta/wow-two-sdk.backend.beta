namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>Defines a failure result carrying a message and a <see cref="FailureCategory"/>.</summary>
public interface ICategorizedFailure : IFailureResult
{
    /// <summary>Gets the human-readable error message for the failure.</summary>
    string ErrorMessage { get; }

    /// <summary>Gets the category that classifies the failure.</summary>
    FailureCategory Category { get; }
}
