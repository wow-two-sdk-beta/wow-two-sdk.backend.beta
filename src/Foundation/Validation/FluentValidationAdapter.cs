namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Adapts FluentValidation validators to the <see cref="IValidator{T}"/> contract.</summary>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class FluentValidationAdapter<T> : IValidator<T>
{
    private readonly FluentValidation.IValidator<T>[] _validators;

    /// <summary>Initializes the adapter with the FluentValidation validators registered for <typeparamref name="T"/>.</summary>
    /// <param name="validators">The underlying FluentValidation validators.</param>
    public FluentValidationAdapter(IEnumerable<FluentValidation.IValidator<T>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);

        _validators = validators.ToArray();
    }

    /// <inheritdoc />
    public ValidationError? Validate(T instance)
    {
        if (_validators.Length == 0)
        {
            return null;
        }

        var context = new FluentValidation.ValidationContext<T>(instance);
        var failures = _validators
            .SelectMany(validator => validator.Validate(context).Errors)
            .Select(failure => new FieldError
            {
                Property = failure.PropertyName,
                Message = failure.ErrorMessage,
                Code = failure.ErrorCode ?? string.Empty,
            })
            .ToArray();

        return failures.Length == 0
            ? null
            : ValidationError.From(failures);
    }

    /// <inheritdoc />
    public void ValidateAndThrow(T instance)
    {
        var error = Validate(instance);
        if (error is not null)
        {
            throw new ValidationException(error);
        }
    }
}
