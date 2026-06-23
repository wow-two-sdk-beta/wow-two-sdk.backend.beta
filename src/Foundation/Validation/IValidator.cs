namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Defines the contract for validating an instance of <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Validates <paramref name="instance"/>, returning the aggregate error or <c>null</c> when valid.</summary>
    /// <param name="instance">The instance to validate.</param>
    ValidationError? Validate(T instance);

    /// <summary>Validates <paramref name="instance"/> and throws <see cref="ValidationException"/> when invalid.</summary>
    /// <param name="instance">The instance to validate.</param>
    void ValidateAndThrow(T instance);
}
