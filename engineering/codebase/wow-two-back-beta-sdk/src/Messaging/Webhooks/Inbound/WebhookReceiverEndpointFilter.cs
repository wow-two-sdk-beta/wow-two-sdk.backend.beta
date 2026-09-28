using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>
/// Handles the gate of a webhook endpoint: an unverified delivery answers <c>401</c> (<c>413</c> when too large) before
/// the handler runs, and a delivery id already handled answers with the first status instead of running it again.
/// </summary>
/// <remarks>
/// A handled id is remembered only after a success, so a failed attempt stays retryable; a repeat racing the first
/// answers <c>409</c> and the sender retries later. The id store is the registered <see cref="IIdempotencyRepository"/>.
/// </remarks>
public sealed class WebhookReceiverEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        var receivers = http.RequestServices.GetRequiredService<WebhookReceiverService>();
        var receipt = await receivers.ReceiveAsync(http, http.RequestAborted);
        if (receipt.Failure == WebhookSignatureFailure.TooLarge)
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "The webhook body is too large.");
        if (!receipt.Verified)
            throw AppError.Of(AppErrorType.Unauthorized, "The webhook signature is not valid.", new Dictionary<string, object?>(StringComparer.Ordinal) { ["messageKey"] = "WebhookSignatureInvalid" }).ToException();

        var settings = receivers.Settings(receipt.Receiver);
        if (!settings.Deduplicate || string.IsNullOrEmpty(receipt.DeliveryId) || http.RequestServices.GetService<IIdempotencyRepository>() is not { } repository)
            return await next(context);

        var key = $"webhooks:{receipt.Receiver.ToLowerInvariant()}:{receipt.DeliveryId}";
        var (acquired, handled, ownership) = await repository.TryAcquireAsync(key, typeof(int), http.RequestAborted);
        if (!acquired)
            return handled is int status
                ? Results.StatusCode(status)
                : throw AppErrorFactory.Conflict("This webhook delivery is still being handled.").ToException();

        object? result;
        try
        {
            result = await next(context);
        }
        catch
        {
            await repository.ReleaseAsync(key, ownership, CancellationToken.None);
            throw;
        }

        var statusCode = result is IStatusCodeHttpResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;
        if (statusCode < StatusCodes.Status400BadRequest)
            await repository.StoreAsync(key, ownership, statusCode, settings.DeduplicationWindow, CancellationToken.None);
        else
            await repository.ReleaseAsync(key, ownership, CancellationToken.None);

        return result;
    }
}
