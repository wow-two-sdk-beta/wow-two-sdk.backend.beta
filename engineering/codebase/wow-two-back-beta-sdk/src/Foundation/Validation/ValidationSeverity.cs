namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>How much weight a <see cref="FieldError"/> carries — whether it blocks the operation or only advises.</summary>
/// <remarks>
/// Only <see cref="Error"/> fails a request. <see cref="Warning"/> and <see cref="Info"/> are advisory: they are dropped from
/// <see cref="IValidator{T}.Validate"/>'s verdict and surface through <see cref="IValidator{T}.Inspect"/>, which is what an
/// advisory endpoint reports while the user is still typing.
/// </remarks>
public enum ValidationSeverity
{
    /// <summary>Blocks the operation.</summary>
    Error = 0,

    /// <summary>Advises against the value without blocking it — a weak password, a suspiciously short description.</summary>
    Warning = 1,

    /// <summary>Informational only.</summary>
    Info = 2,
}
