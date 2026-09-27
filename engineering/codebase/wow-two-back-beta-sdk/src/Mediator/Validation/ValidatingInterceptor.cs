using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Validation;

/// <summary>Validates each request through every <see cref="IValidator{T}"/> and throws one aggregate failure when it is invalid.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="validators">The validators applied to each request.</param>
/// <param name="advisories">The optional scope tracker that receives warnings and suggestions.</param>
/// <remarks>
///   - each validator is inspected once; errors block the request, advisories travel with its success
///   - without a registered tracker, advisories are dropped as before
/// </remarks>
public sealed class ValidatingInterceptor<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    IValidationAdvisoryTracker? advisories = null)
    : IRequestInterceptor<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    /// <param name="request">The request flowing through the pipeline.</param>
    /// <param name="nextStep">The continuation that invokes the next behavior or the handler.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextStep);

        FieldError[] failures = validators
            .SelectMany(validator => validator.Inspect(request))
            .ToArray();
        FieldError[] errors = failures
            .Where(failure => failure.Severity == ValidationSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new ValidationException(ValidationError.From(errors));
        }

        advisories?.Record(failures);
        return await nextStep().ConfigureAwait(false);
    }
}
