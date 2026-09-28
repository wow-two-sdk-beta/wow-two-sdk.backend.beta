using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Completes a passkey registration with the credential the browser created.</summary>
public sealed record PasskeyRegistrationApiRequest
{
    /// <summary>The state from <c>manage/passkeys/options</c>.</summary>
    public required string State { get; init; }

    /// <summary>The <c>PublicKeyCredential</c> from <c>navigator.credentials.create</c>, as <c>toJSON()</c> renders it.</summary>
    public required JsonElement Credential { get; init; }

    /// <summary>A label, such as the device; null names it "Passkey".</summary>
    public string? Name { get; init; }
}
