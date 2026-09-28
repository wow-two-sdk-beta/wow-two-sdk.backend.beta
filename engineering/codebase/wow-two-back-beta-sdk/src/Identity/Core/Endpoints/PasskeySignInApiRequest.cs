using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Completes a passkey sign-in with the assertion the browser signed.</summary>
public sealed record PasskeySignInApiRequest
{
    /// <summary>The state from <c>passkeys/login/options</c>.</summary>
    public required string State { get; init; }

    /// <summary>The <c>PublicKeyCredential</c> from <c>navigator.credentials.get</c>, as <c>toJSON()</c> renders it.</summary>
    public required JsonElement Credential { get; init; }
}
