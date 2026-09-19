using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Validation;

/// <summary>Validates each request through every <see cref="IValidator{T}"/> and throws one aggregate failure when it is invalid.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="validators">The validators applied to each request.</param>
public sealed class ValidatingInterceptor<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
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

        var errors = validators
            .Select(validator => validator.Validate(request))
            .Where(error => error is not null)
            .Cast<ValidationError>()
            .ToArray();

        if (errors.Length == 1)
        {
            throw new ValidationException(errors[0]);
        }

        if (errors.Length > 1)
        {
            throw new ValidationException(ValidationError.From(
                errors.SelectMany(error => error.Failures).ToArray()));
        }

        return await nextStep().ConfigureAwait(false);
    }
}
