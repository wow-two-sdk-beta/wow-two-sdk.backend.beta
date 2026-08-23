namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Adapts FluentValidation validators to the <see cref="IValidator{T}"/> contract.</summary>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class FluentValidationAdapter<T> : IValidator<T>
{
    /// <summary>Placeholder keys never published, whatever the validator declares.</summary>
    /// <remarks>
    /// <c>PropertyValue</c> is the REJECTED VALUE, which FluentValidation hangs on every failure — publishing it echoes a rejected password
    /// into the response and from there into logs. <c>PropertyPath</c> duplicates <see cref="FieldError.Property"/>. Per-member declarations
    /// (<see cref="ISensitiveMembers"/>) widen this; nothing narrows it.
    /// </remarks>
    private static readonly string[] NeverPublishedKeys = ["PropertyValue", "PropertyPath"];

    private readonly FluentValidation.IValidator<T>[] _validators;
    private readonly HashSet<string> _sensitiveMembers;

    /// <summary>Initializes the adapter with the FluentValidation validators registered for <typeparamref name="T"/>.</summary>
    /// <param name="validators">The underlying FluentValidation validators.</param>
    public FluentValidationAdapter(IEnumerable<FluentValidation.IValidator<T>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);

        _validators = validators.ToArray();
        _sensitiveMembers = new HashSet<string>(StringComparer.Ordinal);

        // Unioned across every validator for T — each declares the members ITS operation must not expose.
        foreach (var declaring in _validators.OfType<ISensitiveMembers>())
        {
            _sensitiveMembers.UnionWith(declaring.SensitiveMembers);
        }
    }

    /// <inheritdoc />
    public ValidationError? Validate(T instance)
    {
        var failures = Collect(instance)
            .Where(failure => failure.Severity == ValidationSeverity.Error)
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

    /// <inheritdoc />
    public IReadOnlyList<FieldError> Inspect(T instance) => Collect(instance);

    /// <summary>Runs every registered validator and projects each failure onto a <see cref="FieldError"/>.</summary>
    /// <param name="instance">The instance to validate.</param>
    private FieldError[] Collect(T instance)
    {
        if (_validators.Length == 0)
        {
            return [];
        }

        var context = new FluentValidation.ValidationContext<T>(instance);

        return _validators
            .SelectMany(validator => validator.Validate(context).Errors)
            .Select(failure => new FieldError
            {
                Property = failure.PropertyName,
                Message = failure.ErrorMessage,
                Code = failure.ErrorCode ?? string.Empty,
                Params = ExtractParams(failure),
                Severity = ToSeverity(failure.Severity),
            })
            .ToArray();
    }

    /// <summary>Extracts a failure's rule operands, or <see langword="null"/> when the member is declared sensitive.</summary>
    /// <param name="failure">The FluentValidation failure to read.</param>
    private Dictionary<string, object>? ExtractParams(FluentValidation.Results.ValidationFailure failure)
    {
        var placeholders = failure.FormattedMessagePlaceholderValues;
        if (placeholders is null || placeholders.Count == 0 || _sensitiveMembers.Contains(failure.PropertyName))
        {
            return null;
        }

        var operands = placeholders
            .Where(entry => !NeverPublishedKeys.Contains(entry.Key, StringComparer.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return operands.Count == 0 ? null : operands;
    }

    /// <summary>Maps FluentValidation's severity onto the SDK's, keeping the library behind the adapter.</summary>
    /// <param name="severity">The FluentValidation severity.</param>
    private static ValidationSeverity ToSeverity(FluentValidation.Severity severity) => severity switch
    {
        FluentValidation.Severity.Warning => ValidationSeverity.Warning,
        FluentValidation.Severity.Info => ValidationSeverity.Info,
        _ => ValidationSeverity.Error,
    };
}
