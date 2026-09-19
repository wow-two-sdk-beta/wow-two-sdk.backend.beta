namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Holds configuration for what <c>AddMediator</c> wires beside the dispatcher.</summary>
public sealed record MediatorOptions
{
    /// <summary>Gets or sets whether a handler exception comes back as an <c>AppResult.Failure</c>. Default on.</summary>
    /// <remarks>
    ///   - off leaves the exception to propagate, for a host whose retry reads exceptions
    ///   - a request whose response carries no failure arm rethrows either way
    /// </remarks>
    public bool ExceptionToResult { get; set; } = true;
}
