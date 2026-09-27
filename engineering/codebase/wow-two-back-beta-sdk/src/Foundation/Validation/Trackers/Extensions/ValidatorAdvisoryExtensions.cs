namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers.Extensions;

/// <summary>Extends validators that run inside a handler with advisory tracking.</summary>
public static class ValidatorAdvisoryExtensions
{
    /// <summary>Validates <paramref name="instance"/> once, recording its warnings and suggestions when it passes.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator to run.</param>
    /// <param name="instance">The value to validate.</param>
    /// <param name="advisories">The scope tracker that receives the advisories of a passing instance.</param>
    /// <returns>The aggregate error when any failure blocks; otherwise <see langword="null"/>.</returns>
    /// <remarks>
    ///   - for validation that needs loaded state, such as a persisted mode; request validators run in the mediator interceptor
    ///   - a blocked instance records nothing, as the interceptor does
    /// </remarks>
    public static ValidationError? Validate<T>(this IValidator<T> validator, T instance, IValidationAdvisoryTracker advisories)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(advisories);

        IReadOnlyList<FieldError> failures = validator.Inspect(instance);
        FieldError[] errors = [.. failures.Where(static failure => failure.Severity == ValidationSeverity.Error)];
        if (errors.Length > 0)
        {
            return ValidationError.From(errors);
        }

        advisories.Record(failures);
        return null;
    }
}
