using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Default <see cref="IWebhookPublisher"/> — resolves matching subscriptions and fans a signed delivery out to each.</summary>
internal sealed class WebhookPublisher(IWebhookSubscriptionStore store, HttpWebhookDispatcher dispatcher) : IWebhookPublisher
{
    public async ValueTask PublishAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);

        var subscriptions = await store.GetMatchingAsync(eventType, cancellationToken);
        foreach (var subscription in subscriptions)
            await dispatcher.DeliverAsync(subscription, eventType, payload, cancellationToken);
    }

    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Deliberately NOT routed through IMessageSerializer / IMessageTypeResolver, unlike the transports and the outbox:
        // both seams change bytes that leave the process and that subscribers already depend on.
        //   - Body: the seam's default serializer uses JsonOptionsPresets (camelCase, null-omitting, relaxed escaping,
        //     NodaTime converters); this call uses JsonSerializerOptions.Default (PascalCase, nulls written, strict
        //     escaping). Different bytes mean a different signed body (WebhookSignature signs the payload verbatim) and
        //     a renamed JSON property on every field, breaking every existing subscriber's parser.
        //   - Event type: the resolver emits a FullName token ("MyApp.Events.OrderPlaced"); this sends Type.Name
        //     ("OrderPlaced"), which is what subscribers' EventTypeFilter globs and X-Webhook-Event routing match on.
        // Either swap is a subscriber-visible contract break, so it needs an opt-in switch or a v2 signature scheme.
        // Callers wanting another format serialize themselves and use the raw-payload overload.
        var payload = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        return PublishAsync(typeof(TEvent).Name, payload, cancellationToken);
    }
}

/// <summary>
/// Delivers a signed webhook request over <c>IHttpClientFactory</c> with a bounded retry budget: transient failures
/// (5xx / 408 / timeout / connection error) are retried per <see cref="IRetryPolicy"/>; permanent failures (other 4xx)
/// are not. On success or terminal drop it reports to <see cref="IWebhookDeliveryLog"/>; on exhaustion it logs and drops.
/// </summary>
internal sealed partial class HttpWebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    IOptions<WebhookOptions> options,
    IRetryPolicy retryPolicy,
    IWebhookDeliveryLog deliveryLog,
    TimeProvider timeProvider,
    ILogger<HttpWebhookDispatcher> logger)
{
    private const int HttpRequestTimeout = 408;

    /// <summary>Deliver <paramref name="payload"/> to one subscription, signing and retrying per the configured budget.</summary>
    /// <param name="subscription">The target subscription.</param>
    /// <param name="eventType">The event type (sent as <see cref="WebhookHeaders.Event"/>).</param>
    /// <param name="payload">The request body.</param>
    /// <param name="cancellationToken">Cancellation token — cancellation propagates without retry or drop.</param>
    public async ValueTask DeliverAsync(WebhookSubscription subscription, string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var opt = options.Value;

        // SSRF pre-flight (scheme + host allowlist). Address-level blocking (private/loopback/link-local, incl.
        // DNS-rebinding) is enforced at connect time by the guarded HttpClient handler; see WebhookAddressPolicy.
        if (!WebhookAddressPolicy.IsSchemeAllowed(subscription.Url, opt.RequireHttps)
            || !WebhookAddressPolicy.IsHostAllowed(subscription.Url, opt.AllowedHosts))
        {
            LogRejected(subscription.Url, eventType, null);
            await RecordAsync(subscription, eventType, WebhookDeliveryOutcome.Dropped, 0, null, cancellationToken);
            return;
        }

        var retryConfig = new RetryConfig(opt.MaxAttempts, BackoffKind.ExponentialJitter, opt.BaseRetryDelay, opt.MaxRetryDelay);
        var client = httpClientFactory.CreateClient(WebhookDefaults.HttpClientName);

        var attempts = 0;
        int? lastStatus = null;
        while (true)
        {
            attempts++;
            var (outcome, status) = await TrySendAsync(client, subscription, eventType, payload, opt.RequestTimeout, cancellationToken);
            if (status is not null)
                lastStatus = status;

            switch (outcome)
            {
                case SendOutcome.Success:
                    await RecordAsync(subscription, eventType, WebhookDeliveryOutcome.Delivered, attempts, lastStatus, cancellationToken);
                    return;

                case SendOutcome.Permanent:
                    LogRejected(subscription.Url, eventType, status);
                    await RecordAsync(subscription, eventType, WebhookDeliveryOutcome.Dropped, attempts, lastStatus, cancellationToken);
                    return;

                default:
                    var delay = retryPolicy.NextDelay(attempts, retryConfig);
                    if (delay is null)
                    {
                        LogExhausted(subscription.Url, eventType, attempts);
                        await RecordAsync(subscription, eventType, WebhookDeliveryOutcome.Dropped, attempts, lastStatus, cancellationToken);
                        return;
                    }

                    LogRetrying(subscription.Url, eventType, attempts, status);
                    await Task.Delay(delay.Value, timeProvider, cancellationToken);
                    break;
            }
        }
    }

    private async ValueTask<(SendOutcome Outcome, int? StatusCode)> TrySendAsync(
        HttpClient client,
        WebhookSubscription subscription,
        string eventType,
        ReadOnlyMemory<byte> payload,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = WebhookSignature.Create(subscription.Secret, timestamp, payload.Span);

        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
        {
            Content = new ReadOnlyMemoryContent(payload),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation(WebhookHeaders.Signature, signature);
        request.Headers.TryAddWithoutValidation(WebhookHeaders.Timestamp, timestamp);
        request.Headers.TryAddWithoutValidation(WebhookHeaders.Event, eventType);
        request.Headers.TryAddWithoutValidation(WebhookHeaders.Id, Guid.NewGuid().ToString("N"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var response = await client.SendAsync(request, timeoutCts.Token);
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
        catch (HttpRequestException ex) when (ex.InnerException is WebhookAddressBlockedException)
        {
            return (SendOutcome.Permanent, null); // target resolved to a blocked (private) address — never retry
        }
        catch (HttpRequestException)
        {
            return (SendOutcome.Transient, null); // connection / protocol failure
        }
    }

    private ValueTask RecordAsync(WebhookSubscription subscription, string eventType, WebhookDeliveryOutcome outcome, int attempts, int? statusCode, CancellationToken cancellationToken)
        => deliveryLog.RecordAsync(
            new WebhookDeliveryRecord(subscription.Id, eventType, subscription.Url, outcome, attempts, statusCode, timeProvider.GetUtcNow()),
            cancellationToken);

    // EventIds 6901–6999 are the webhooks block. Messaging owns 6xxx and the broker adapters march up it one hundred at
    // a time (6301 RabbitMQ · 6401 Kafka · 6501 NATS · 6601+ free for the next adapter), so webhooks — not an adapter —
    // sits at the top of the range, clear of that march and below Foundation.Security at 7001. The block was 6401–6403,
    // which collided with KafkaTransport.
    [LoggerMessage(EventId = 6901, Level = LogLevel.Debug, Message = "Webhook delivery to {Url} for {EventType} failed (attempt {Attempt}, status {StatusCode}); retrying")]
    private partial void LogRetrying(Uri url, string eventType, int attempt, int? statusCode);

    [LoggerMessage(EventId = 6902, Level = LogLevel.Warning, Message = "Webhook delivery to {Url} for {EventType} rejected with status {StatusCode}; not retrying")]
    private partial void LogRejected(Uri url, string eventType, int? statusCode);

    [LoggerMessage(EventId = 6903, Level = LogLevel.Warning, Message = "Webhook delivery to {Url} for {EventType} exhausted after {Attempts} attempts; dropping")]
    private partial void LogExhausted(Uri url, string eventType, int attempts);
}

/// <summary>Outcome of a single webhook send attempt.</summary>
internal enum SendOutcome
{
    /// <summary>2xx response.</summary>
    Success,

    /// <summary>Retryable failure — 5xx, 408, timeout, or connection error.</summary>
    Transient,

    /// <summary>Non-retryable failure — any other 4xx.</summary>
    Permanent,
}

/// <summary>No-op <see cref="IWebhookDeliveryLog"/> — the default when no delivery observer is registered.</summary>
internal sealed class NoopWebhookDeliveryLog : IWebhookDeliveryLog
{
    public ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
