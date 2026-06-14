namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Adapts FluentValidation validators to the <see cref="IValidator{T}"/> contract.</summary>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class FluentValidationAdapter<T> : IValidator<T>
{
    private readonly FluentValidation.IValidator<T>[] _validators;

    /// <summary>Initializes the adapter with the FluentValidation validators registered for <typeparamref name="T"/>.</summary>
    /// <param name="validators">The underlying FluentValidation validators.</param>
    public FluentValidationAdapter(IEnumerable<FluentValidation.IValidator<T>> validators) =>
        _validators = validators.ToArray();

    /// <inheritdoc />
    public ValidationResult Validate(T instance)
    {
        if (_validators.Length == 0)
            return new ValidationResult.Success();

        var context = new FluentValidation.ValidationContext<T>(instance);
        var errors = _validators
            .SelectMany(validator => validator.Validate(context).Errors)
            .Select(failure => new ValidationError(failure.PropertyName, failure.ErrorMessage, failure.ErrorCode ?? string.Empty))
            .ToArray();

        return errors.Length == 0
            ? new ValidationResult.Success()
            : new ValidationResult.Failure(errors);
    }

    /// <inheritdoc />
    public void ValidateAndThrow(T instance)
    {
        if (Validate(instance) is ValidationResult.Failure failure)
            throw new ValidationException(failure.Errors);
    }
}
