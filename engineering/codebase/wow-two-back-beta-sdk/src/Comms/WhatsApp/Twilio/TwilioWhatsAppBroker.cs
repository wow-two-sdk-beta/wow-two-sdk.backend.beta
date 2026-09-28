using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Twilio;

/// <summary>Integrates Twilio's WhatsApp sender as the WhatsApp provider; a template is a content SID with numbered variables.</summary>
/// <param name="httpClientFactory">Creates the named client registered by <c>AddTwilioWhatsAppBroker</c>.</param>
/// <param name="options">Credentials and sender.</param>
public sealed class TwilioWhatsAppBroker(IHttpClientFactory httpClientFactory, TwilioWhatsAppOptions options) : IWhatsAppBroker
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.whatsapp.twilio";

    /// <inheritdoc />
    public async Task<WhatsAppSendResult> SendAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Template is null && string.IsNullOrEmpty(message.Text))
            return new WhatsAppSendResult { Success = false, FailureReason = "empty_message" };

        var fields = new List<KeyValuePair<string, string>>
        {
            new("To", $"whatsapp:+{WhatsAppServiceCollectionExtensions.ToDigits(message.To)}"),
            new("From", $"whatsapp:+{WhatsAppServiceCollectionExtensions.ToDigits(options.From)}"),
        };
        if (message.Template is { } template)
        {
            var variables = template.BodyParameters
                .Select((value, index) => (Key: (index + 1).ToString(CultureInfo.InvariantCulture), Value: value))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            fields.Add(new("ContentSid", template.Name));
            fields.Add(new("ContentVariables", JsonSerializer.Serialize(variables)));
        }
        else
        {
            fields.Add(new("Body", message.Text!));
        }

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"2010-04-01/Accounts/{Uri.EscapeDataString(options.AccountSid)}/Messages.json")
            {
                Content = new FormUrlEncodedContent(fields),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.AuthToken}")));

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<TwilioMessageResponse>(cancellationToken);
            return response.IsSuccessStatusCode && body?.Sid is { } sid
                ? new WhatsAppSendResult { Success = true, ProviderMessageId = sid }
                : new WhatsAppSendResult { Success = false, FailureReason = $"twilio_{body?.Code ?? (int)response.StatusCode}: {body?.Message}" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new WhatsAppSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    private sealed record TwilioMessageResponse
    {
        [JsonPropertyName("sid")]
        public string? Sid { get; init; }

        [JsonPropertyName("code")]
        public int? Code { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
