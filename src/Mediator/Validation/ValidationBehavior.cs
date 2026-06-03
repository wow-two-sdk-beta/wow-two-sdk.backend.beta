using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Validation;

/// <summary>
/// Pipeline behavior that runs every registered <see cref="IValidator{T}"/> for the request type.
/// Throws <see cref="ValidationException"/> if any rule fails.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IServiceProvider serviceProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var validators = serviceProvider.GetServices<IValidator<TRequest>>().ToArray();
        if (validators.Length == 0)
            return await next().ConfigureAwait(false);

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length > 0)
            throw new ValidationException(failures);

        return await next().ConfigureAwait(false);
    }
}

/// <summary>Registration helper.</summary>
public static class ValidationBehaviorServiceCollectionExtensions
{
    /// <summary>Register the validation pipeline behavior.</summary>
    public static IServiceCollection AddMediatorValidationBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddMediatorBehavior(typeof(ValidationBehavior<,>));
    }
}
