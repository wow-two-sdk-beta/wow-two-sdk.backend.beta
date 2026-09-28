using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>
/// Represents a received webhook: the raw body its signature covered and what the sender stated. A route handler takes it
/// as a parameter and reads the payload with <see cref="ReadJson{T}"/>; <c>RequireWebhookSignature</c> admits only
/// verified receipts, so a handler never sees a failed one.
/// </summary>
public sealed record WebhookReceiptModel
{
    private static readonly JsonSerializerOptions JsonDefaults = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the receiver name.</summary>
    public required string Receiver { get; init; }

    /// <summary>Gets the signature scheme that validated it.</summary>
    public required string Scheme { get; init; }

    /// <summary>Gets why validation failed; <see cref="WebhookSignatureFailure.None"/> for a verified receipt.</summary>
    public required WebhookSignatureFailure Failure { get; init; }

    /// <summary>Gets whether the signature validated within the tolerance.</summary>
    public bool Verified => Failure == WebhookSignatureFailure.None;

    /// <summary>Gets the sender's delivery or event id, when the scheme carries one.</summary>
    public string? DeliveryId { get; init; }

    /// <summary>Gets the event type the sender named, when it names one.</summary>
    public string? EventType { get; init; }

    /// <summary>Gets the signed or stated time of the delivery, when the scheme carries one.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Gets the raw body, exactly as signed.</summary>
    public required ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Deserializes the body; web defaults (camelCase, case-insensitive) unless <paramref name="options"/> is given.</summary>
    /// <typeparam name="T">The payload shape.</typeparam>
    /// <param name="options">Serializer options.</param>
    public T? ReadJson<T>(JsonSerializerOptions? options = null) => JsonSerializer.Deserialize<T>(Body.Span, options ?? JsonDefaults);

    /// <summary>Binds a route handler parameter: reads and validates the request once, for the endpoint's receiver.</summary>
    /// <param name="context">The request.</param>
    /// <param name="parameter">The bound parameter.</param>
    public static async ValueTask<WebhookReceiptModel?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await context.RequestServices.GetRequiredService<WebhookReceiverService>().ReceiveAsync(context, context.RequestAborted);
    }
}
