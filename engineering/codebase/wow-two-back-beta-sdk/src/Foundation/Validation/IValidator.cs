namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Defines the contract for validating an instance of <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Validates <paramref name="instance"/>, returning the aggregate error or <c>null</c> when valid.</summary>
    /// <param name="instance">The instance to validate.</param>
    /// <remarks>Only <see cref="ValidationSeverity.Error"/> failures count; advisory ones are dropped. Use <see cref="Inspect"/> to see them.</remarks>
    ValidationError? Validate(T instance);

    /// <summary>Validates <paramref name="instance"/> and throws <see cref="ValidationException"/> when invalid.</summary>
    /// <param name="instance">The instance to validate.</param>
    void ValidateAndThrow(T instance);

    /// <summary>Reports every failure at every severity, without deciding pass or fail.</summary>
    /// <param name="instance">The instance to inspect.</param>
    /// <returns>All failures in rule order; empty when nothing fired.</returns>
    /// <remarks>
    /// The advisory read: warnings a write path ignores, surfaced while the user is still typing. Same rules, same codes, same paths as
    /// <see cref="Validate"/> — the difference is the verdict, which this method does not make.
    /// </remarks>
    IReadOnlyList<FieldError> Inspect(T instance);
}
