using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents an aggregate validation failure carrying its per-field failures.</summary>
public sealed record ValidationError : AppError
{
    /// <summary>Gets the per-field failures.</summary>
    public required IReadOnlyList<FieldError> Failures { get; init; }

    /// <summary>Creates a validation error from <paramref name="failures"/>.</summary>
    /// <param name="failures">The per-field failures.</param>
    public static ValidationError From(IReadOnlyList<FieldError> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        return new ValidationError
        {
            Type = AppErrorType.Validation,
            Message = "One or more validation errors occurred.",
            Failures = failures,
        };
    }
}
