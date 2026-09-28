using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Meta;

/// <summary>Integrates Meta's WhatsApp Cloud API as the WhatsApp provider.</summary>
/// <param name="httpClientFactory">Creates the named client registered by <c>AddMetaWhatsAppBroker</c>.</param>
/// <param name="options">Access token, sender and API version.</param>
public sealed class MetaWhatsAppBroker(IHttpClientFactory httpClientFactory, MetaWhatsAppOptions options) : IWhatsAppBroker
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.whatsapp.meta";

    /// <inheritdoc />
    public async Task<WhatsAppSendResult> SendAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Template is null && string.IsNullOrEmpty(message.Text))
            return new WhatsAppSendResult { Success = false, FailureReason = "empty_message" };

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{Uri.EscapeDataString(options.ApiVersion)}/{Uri.EscapeDataString(options.PhoneNumberId)}/messages")
            {
                Content = JsonContent.Create(Payload(message)),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<CloudApiResponse>(cancellationToken);
            return response.IsSuccessStatusCode && body?.Messages is [{ Id: { } id }, ..]
                ? new WhatsAppSendResult { Success = true, ProviderMessageId = id }
                : new WhatsAppSendResult { Success = false, FailureReason = $"meta_{body?.Error?.Code ?? (int)response.StatusCode}: {body?.Error?.Message}" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new WhatsAppSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    private static JsonObject Payload(WhatsAppMessage message)
    {
        var payload = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = WhatsAppServiceCollectionExtensions.ToDigits(message.To),
        };

        if (message.Template is not { } template)
        {
            payload["type"] = "text";
            payload["text"] = new JsonObject { ["preview_url"] = false, ["body"] = message.Text };
            return payload;
        }

        var components = new JsonArray();
        if (template.BodyParameters.Count > 0)
            components.Add(new JsonObject { ["type"] = "body", ["parameters"] = TextParameters(template.BodyParameters) });
        if (template.ButtonParameter is { } button)
            components.Add(new JsonObject { ["type"] = "button", ["sub_type"] = "url", ["index"] = "0", ["parameters"] = TextParameters([button]) });

        payload["type"] = "template";
        payload["template"] = new JsonObject
        {
            ["name"] = template.Name,
            ["language"] = new JsonObject { ["code"] = template.LanguageCode },
            ["components"] = components,
        };
        return payload;
    }

    private static JsonArray TextParameters(IEnumerable<string> values)
        => [.. values.Select(value => (JsonNode)new JsonObject { ["type"] = "text", ["text"] = value })];

    private sealed record CloudApiResponse
    {
        [JsonPropertyName("messages")]
        public IReadOnlyList<CloudApiMessage>? Messages { get; init; }

        [JsonPropertyName("error")]
        public CloudApiError? Error { get; init; }
    }

    private sealed record CloudApiMessage
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed record CloudApiError
    {
        [JsonPropertyName("code")]
        public int? Code { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
