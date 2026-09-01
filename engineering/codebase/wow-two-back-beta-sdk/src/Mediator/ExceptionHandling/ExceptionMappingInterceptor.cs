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
public sealed class ExceptionMappingInterceptor<TRequest, TResponse>(ErrorRecordingService observer, IExceptionMapper exceptionMapper)
    : IRequestInterceptor<TRequest, TResponse>
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
        catch (OperationCanceledException cancellation) when (cancellationToken.IsCancellationRequested)
        {
            return Recover(AppErrorFactory.Canceled(), cancellation);
        }
        catch (OperationCanceledException timeout)
        {
            return Recover(AppErrorFactory.OperationTimeout(), timeout);
        }
        catch (Exception exception) when (exception is not (NullReferenceException
            or ObjectDisposedException or StackOverflowException or OutOfMemoryException))
        {
            AppError error;
            try
            {
                error = exceptionMapper.Map(exception);
            }
            catch (Exception mapping) when (mapping is not OperationCanceledException)
            {
                error = AppErrorFactory.Unexpected(inner: exception);
            }

            return Recover(error, exception);
        }
    }

    /// <summary>Records the failure and returns it, so a fault in recording cannot become the response.</summary>
    /// <param name="error">The mapped failure.</param>
    /// <param name="exception">The exception it was mapped from.</param>
    private TResponse Recover(AppError error, Exception exception)
    {
        try
        {
            observer.Record(error, exception);
        }
        catch (Exception recording) when (recording is not OperationCanceledException)
        {
            // Observability is a side effect of producing the failure; the failure is already built.
        }

        return Convert(error, exception);
    }

    private static TResponse Convert(AppError error, Exception cause)
    {
        if (AppResultFactory.TryCreateFailure<TResponse>(error, out var failure))
        {
            return failure;
        }

        // A response with no failure arm keeps the original: a caller awaiting a bare value still reads
        // cancellation as OperationCanceledException, which is what every await in the framework expects.
        if (cause is OperationCanceledException)
        {
            throw cause;
        }

        throw error.ToException(cause);
    }
}
