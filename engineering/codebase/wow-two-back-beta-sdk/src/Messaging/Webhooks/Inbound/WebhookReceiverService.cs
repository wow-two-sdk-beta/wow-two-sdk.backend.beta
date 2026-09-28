using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>
/// Provides inbound webhook reception: reads the raw body under the receiver's size cap, validates it with the receiver's
/// scheme, and remembers the receipt on the request, so the parameter binding and the endpoint filter share one result.
/// </summary>
/// <param name="validators">Resolves the scheme's validator.</param>
/// <param name="options">The receivers; <c>Webhooks:Inbound</c> reloads live.</param>
/// <param name="time">The clock the replay window is measured against.</param>
/// <param name="logger">Records rejected deliveries.</param>
public sealed partial class WebhookReceiverService(
    IWebhookSignatureValidatorFactory validators,
    IOptionsMonitor<InboundWebhookOptions> options,
    TimeProvider time,
    ILogger<WebhookReceiverService> logger)
{
    /// <summary>Receives the request for the receiver its endpoint names through <see cref="WebhookReceiverAttribute"/>.</summary>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The endpoint names no receiver.</exception>
    public Task<WebhookReceiptModel> ReceiveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var receiver = context.GetEndpoint()?.Metadata.GetMetadata<WebhookReceiverAttribute>()?.Receiver
            ?? throw new InvalidOperationException("The endpoint names no webhook receiver; add .RequireWebhookSignature(\"name\").");
        return ReceiveAsync(context, receiver, cancellationToken);
    }

    /// <summary>Receives the request for <paramref name="receiver"/>; a second call on the same request returns the first receipt.</summary>
    /// <param name="context">The request.</param>
    /// <param name="receiver">The receiver name under <c>Webhooks:Inbound:Receivers</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The receiver is not configured.</exception>
    public async Task<WebhookReceiptModel> ReceiveAsync(HttpContext context, string receiver, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiver);
        if (context.Features.Get<WebhookReceiptModel>() is { } received && string.Equals(received.Receiver, receiver, StringComparison.OrdinalIgnoreCase))
            return received;

        var settings = Settings(receiver);
        var scheme = string.IsNullOrWhiteSpace(settings.Scheme) ? receiver : settings.Scheme;
        var validator = validators.Create(scheme);
        var body = await ReadBodyAsync(context.Request, settings.MaxBodyBytes, cancellationToken);
        var result = body is null
            ? WebhookSignatureResult.Failed(WebhookSignatureFailure.TooLarge)
            : validator.Validate(context.Request.Headers, body, settings, time.GetUtcNow());
        if (!result.Succeeded)
            LogRejected(logger, receiver, scheme, result.Failure);

        var receipt = new WebhookReceiptModel
        {
            Receiver = receiver,
            Scheme = scheme,
            Failure = result.Failure,
            DeliveryId = result.DeliveryId,
            EventType = result.EventType,
            Timestamp = result.Timestamp,
            Body = body ?? ReadOnlyMemory<byte>.Empty,
        };
        context.Features.Set(receipt);
        return receipt;
    }

    /// <summary>The configured receiver.</summary>
    /// <param name="receiver">The receiver name.</param>
    /// <exception cref="InvalidOperationException">The receiver is not configured.</exception>
    public WebhookReceiverOptions Settings(string receiver)
        => options.CurrentValue.Receivers.TryGetValue(receiver, out var settings)
            ? settings
            : throw new InvalidOperationException($"No inbound webhook receiver '{receiver}' is configured under {InboundWebhookOptions.SectionName}:Receivers.");

    /// <summary>The body, or null when it exceeds <paramref name="limit"/>; a buffered body is rewound for later readers.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, int limit, CancellationToken cancellationToken)
    {
        if (request.ContentLength > limit)
            return null;

        request.EnableBuffering(bufferThreshold: 64 * 1024);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
                return null;

            buffer.Write(chunk, 0, read);
        }

        request.Body.Position = 0;
        return buffer.ToArray();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected an inbound webhook for receiver {Receiver} (scheme {Scheme}): {Failure}.")]
    private static partial void LogRejected(ILogger logger, string receiver, string scheme, WebhookSignatureFailure failure);
}
