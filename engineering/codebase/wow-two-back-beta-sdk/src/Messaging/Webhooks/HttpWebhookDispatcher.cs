using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Http.Safety;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>
/// Delivers a signed webhook request over <c>IHttpClientFactory</c> with a bounded retry budget: transient failures
/// (5xx / 408 / timeout / connection error) are retried per <see cref="IRetryPolicy"/>; permanent failures (other 4xx)
/// are not. On success or terminal drop it reports to <see cref="IWebhookDeliveryLoggingService"/>; on exhaustion it logs and drops.
/// </summary>
internal sealed partial class HttpWebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    WebhookOptions options,
    IRetryPolicy retryPolicy,
    IWebhookDeliveryLoggingService deliveryLogging,
    IWebhookSignatureHasher signatureHasher,
    TimeProvider timeProvider,
    ILogger<HttpWebhookDispatcher> logger)
{
    private const int HttpRequestTimeout = 408;

    /// <summary>Deliver <paramref name="payload"/> to one subscription, signing and retrying per the configured budget.</summary>
    /// <param name="subscription">The target subscription.</param>
    /// <param name="eventType">The event type (sent as <see cref="WebhookHeaderConstants.Event"/>).</param>
    /// <param name="payload">The request body.</param>
    /// <param name="cancellationToken">Cancellation token — cancellation propagates without retry or drop.</param>
    /// <param name="deliveryId">The delivery id every attempt carries; null starts a new delivery.</param>
    public async ValueTask<WebhookDeliveryOutcome> DeliverAsync(WebhookSubscription subscription, string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken, string? deliveryId = null)
    {
        var opt = options;
        var delivery = new Delivery { Id = deliveryId ?? Guid.NewGuid().ToString("N"), Subscription = subscription, EventType = eventType, Payload = payload };

        // Validate scheme and host here; the guarded handler blocks unsafe resolved addresses at connect time.
        if (!WebhookAddressMapper.IsSchemeAllowed(subscription.Url, opt.RequireHttps)
            || !WebhookAddressMapper.IsHostAllowed(subscription.Url, opt.AllowedHosts))
        {
            LogRejected(subscription.Url, eventType, null);
            return await RecordAsync(delivery, WebhookDeliveryOutcome.Dropped, 0, null, cancellationToken);
        }

        var retryConfig = new RetryConfig { MaxAttempts = opt.MaxAttempts, Backoff = BackoffKind.ExponentialJitter, BaseDelay = opt.BaseRetryDelay, MaxDelay = opt.MaxRetryDelay };
        using var client = httpClientFactory.CreateClient(WebhookDefaultConstants.HttpClientName);

        var attempts = 0;
        int? lastStatus = null;
        while (true)
        {
            attempts++;
            var (outcome, status) = await TrySendAsync(client, delivery, opt.RequestTimeout, cancellationToken);
            if (status is not null)
                lastStatus = status;

            switch (outcome)
            {
                case SendOutcome.Success:
                    return await RecordAsync(delivery, WebhookDeliveryOutcome.Delivered, attempts, lastStatus, cancellationToken);

                case SendOutcome.Permanent:
                    LogRejected(subscription.Url, eventType, status);
                    return await RecordAsync(delivery, WebhookDeliveryOutcome.Dropped, attempts, lastStatus, cancellationToken);

                default:
                    var delay = retryPolicy.NextDelay(attempts, retryConfig);
                    if (delay is null)
                    {
                        LogExhausted(subscription.Url, eventType, attempts);
                        return await RecordAsync(delivery, WebhookDeliveryOutcome.Dropped, attempts, lastStatus, cancellationToken);
                    }

                    LogRetrying(subscription.Url, eventType, attempts, status);
                    await Task.Delay(delay.Value, timeProvider, cancellationToken);
                    break;
            }
        }
    }

    private async ValueTask<(SendOutcome Outcome, int? StatusCode)> TrySendAsync(
        HttpClient client,
        Delivery delivery,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var (subscription, eventType, payload) = (delivery.Subscription, delivery.EventType, delivery.Payload);
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = signatureHasher.Create(subscription.Secret, timestamp, payload.Span);

        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
        {
            Content = new ReadOnlyMemoryContent(payload),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation(WebhookHeaderConstants.Signature, signature);
        request.Headers.TryAddWithoutValidation(WebhookHeaderConstants.Timestamp, timestamp);
        request.Headers.TryAddWithoutValidation(WebhookHeaderConstants.Event, eventType);
        request.Headers.TryAddWithoutValidation(WebhookHeaderConstants.Id, delivery.Id);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return (SendOutcome.Success, status);
            if (status >= 500 || status == HttpRequestTimeout)
                return (SendOutcome.Transient, status);
            return (SendOutcome.Permanent, status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // caller cancelled — propagate, no retry, no drop
        }
        catch (OperationCanceledException)
        {
            return (SendOutcome.Transient, null); // per-attempt timeout
        }
        catch (HttpRequestException ex) when (ex is OutboundAddressBlockedException
            || ex.InnerException is OutboundAddressBlockedException)
        {
            return (SendOutcome.Permanent, null); // target resolved to a blocked (private) address — never retry
        }
        catch (HttpRequestException)
        {
            return (SendOutcome.Transient, null); // connection / protocol failure
        }
    }

    private async ValueTask<WebhookDeliveryOutcome> RecordAsync(Delivery delivery, WebhookDeliveryOutcome outcome, int attempts, int? statusCode, CancellationToken cancellationToken)
    {
        await deliveryLogging.RecordAsync(
            new WebhookDeliveryRecord
            {
                DeliveryId = delivery.Id,
                Payload = delivery.Payload,
                SubscriptionId = delivery.Subscription.Id,
                EventType = delivery.EventType,
                Url = delivery.Subscription.Url,
                Outcome = outcome,
                Attempts = attempts,
                StatusCode = statusCode,
                OccurredAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);
        return outcome;
    }

    /// <summary>Represents one delivery: its stable id, target, event and body.</summary>
    private sealed record Delivery
    {
        public required string Id { get; init; }

        public required WebhookSubscription Subscription { get; init; }

        public required string EventType { get; init; }

        public required ReadOnlyMemory<byte> Payload { get; init; }
    }

    // Reserve 6901–6999 for webhooks above broker adapters and below Foundation.Security.
    [LoggerMessage(EventId = 6901, Level = LogLevel.Debug, Message = "Webhook delivery to {Url} for {EventType} failed (attempt {Attempt}, status {StatusCode}); retrying")]
    private partial void LogRetrying(Uri url, string eventType, int attempt, int? statusCode);

    [LoggerMessage(EventId = 6902, Level = LogLevel.Warning, Message = "Webhook delivery to {Url} for {EventType} rejected with status {StatusCode}; not retrying")]
    private partial void LogRejected(Uri url, string eventType, int? statusCode);

    [LoggerMessage(EventId = 6903, Level = LogLevel.Warning, Message = "Webhook delivery to {Url} for {EventType} exhausted after {Attempts} attempts; dropping")]
    private partial void LogExhausted(Uri url, string eventType, int attempts);
}
