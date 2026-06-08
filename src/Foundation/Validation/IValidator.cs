namespace WoW.Two.Sdk.Backend.Beta.Validation;

/// <summary>Defines a validator for instances of <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The type being validated.</typeparam>
public interface IValidator<T>
{
    /// <summary>Validates the instance and returns the outcome without throwing.</summary>
    /// <param name="instance">The instance to validate.</param>
    /// <returns>A success, or a failure carrying the errors found.</returns>
    ValidationResult Validate(T instance);

    /// <summary>Validates the instance and throws <see cref="ValidationException"/> when any rule fails.</summary>
    /// <param name="instance">The instance to validate.</param>
    void ValidateAndThrow(T instance);
}
