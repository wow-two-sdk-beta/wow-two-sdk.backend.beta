using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.TelegramGateway;

/// <summary>
/// Handles <see cref="OtpDeliveryEnvelopeModel"/> deliveries through the Telegram Gateway API, which sends the code to
/// the Telegram account behind a phone number; the delivery address is an E.164 number and the code must be 4–8 digits.
/// </summary>
/// <param name="httpClientFactory">Creates the named client registered by <c>AddTelegramGatewayOtpDelivery</c>.</param>
/// <param name="gatewayOptions">Token and sender.</param>
/// <param name="otpOptions">OTP settings, for the code lifetime sent as the TTL.</param>
public sealed class TelegramGatewayOtpDeliveryHandler(
    IHttpClientFactory httpClientFactory,
    TelegramGatewayOtpOptions gatewayOptions,
    OtpOptions otpOptions) : IOtpDeliveryHandler
{
    /// <summary>The named <see cref="HttpClient"/> this handler uses.</summary>
    public const string HttpClientName = "wow2.otp.telegram-gateway";

    /// <inheritdoc />
    public async Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelopeModel envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.DeliveryAddress))
            return new OtpDeliveryResult { Success = false, FailureReason = "invalid_phone_number" };
        if (envelope.Code.Length is < 4 or > 8 || !envelope.Code.All(char.IsAsciiDigit))
            return new OtpDeliveryResult { Success = false, FailureReason = "telegram_gateway_needs_4_to_8_digits" };

        var ttl = Math.Clamp((int)(envelope.Lifetime ?? otpOptions.CodeLifetime).TotalSeconds, 30, 3600);
        var payload = new JsonObject
        {
            ["phone_number"] = "+" + envelope.DeliveryAddress.Trim().TrimStart('+'),
            ["code"] = envelope.Code,
            ["ttl"] = ttl,
        };
        if (!string.IsNullOrWhiteSpace(gatewayOptions.SenderUsername))
            payload["sender_username"] = gatewayOptions.SenderUsername;
        if (!string.IsNullOrWhiteSpace(gatewayOptions.CallbackUrl))
            payload["callback_url"] = gatewayOptions.CallbackUrl;

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "sendVerificationMessage") { Content = JsonContent.Create(payload) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gatewayOptions.AccessToken);

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<GatewayResponse>(cancellationToken);
            return body is { Ok: true }
                ? new OtpDeliveryResult { Success = true }
                : new OtpDeliveryResult { Success = false, FailureReason = $"telegram_gateway: {body?.Error ?? ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)}" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new OtpDeliveryResult { Success = false, FailureReason = exception.Message };
        }
    }

    private sealed record GatewayResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
