using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>A started passkey ceremony: the WebAuthn options and the state to send back with the credential.</summary>
public sealed record PasskeyCeremonyDto
{
    /// <summary>The options for <c>PublicKeyCredential.parseCreationOptionsFromJSON</c> or <c>parseRequestOptionsFromJSON</c>.</summary>
    public required JsonElement Options { get; init; }

    /// <summary>The opaque state; it expires with the ceremony.</summary>
    public required string State { get; init; }
}
