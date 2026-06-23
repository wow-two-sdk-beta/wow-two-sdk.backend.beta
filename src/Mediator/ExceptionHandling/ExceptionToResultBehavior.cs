using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Result;
using WoW.Two.Sdk.Backend.Beta.Observability.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.ExceptionHandling;

/// <summary>Converts an exception escaping a handler into an <c>AppResult.Failure</c> so the mediator never throws for result-returning requests.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type (an <c>AppResult&lt;TSuccess&gt;</c> for the conversion to apply).</typeparam>
/// <param name="observer">Records the failure for logging and metrics.</param>
/// <param name="exceptionMapper">Translates the caught exception into an <see cref="AppError"/> (unwrapping <see cref="AppException"/>, applying registered rules, else <c>Unexpected</c>).</param>
public sealed class ExceptionToResultBehavior<TRequest, TResponse>(AppErrorObserver observer, IExceptionMapper exceptionMapper)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextStep);

        try
        {
            return await nextStep().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Convert(AppErrors.Canceled());
        }
        catch (OperationCanceledException)
        {
            return Convert(AppErrors.OperationTimeout());
        }
        catch (Exception exception)
        {
            var error = exceptionMapper.Map(exception);

            observer.Record(error, exception);

            return Convert(error);
        }
    }

    private static TResponse Convert(AppError error)
    {
        if (AppResultFactory.TryCreateFailure<TResponse>(error, out var failure))
        {
            return failure;
        }

        throw error.ToException();
    }
}

/// <summary>Provides registration for the terminal exception-to-result pipeline behavior.</summary>
public static class ExceptionToResultBehaviorServiceCollectionExtensions
{
    /// <summary>Registers the terminal behavior that converts handler exceptions into <c>AppResult.Failure</c> (and the <see cref="IExceptionMapper"/> it depends on). Register outermost (first).</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorExceptionToResultBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionMapping();

        return services.AddMediatorBehavior(typeof(ExceptionToResultBehavior<,>));
    }
}
