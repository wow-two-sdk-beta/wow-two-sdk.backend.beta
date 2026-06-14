namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>Marker for failure-side context (e.g. retry-after hints, validation detail) on a <see cref="AppResult{TSuccess,TFailure}.Failure"/>.</summary>
public interface IApplicationFailureContext;
