using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;

/// <summary>Verifies Google ID tokens against Google's published keys, checking the audience matches a configured OAuth client id.</summary>
/// <remarks>Wraps <see cref="GoogleJsonWebSignature.ValidateAsync(string, GoogleJsonWebSignature.ValidationSettings)"/>; a token with no email, a bad signature, a wrong audience, or expiry resolves to <c>null</c>. Tests swap the seam in.</remarks>
public sealed class GoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    private readonly GoogleIdTokenVerifierOptions _options;
    private readonly ILogger<GoogleIdTokenVerifier> _logger;

    /// <summary>Creates the verifier over its audience options and a logger.</summary>
    /// <param name="options">Accepted audiences (OAuth client ids).</param>
    /// <param name="logger">Logger for validation failures.</param>
    public GoogleIdTokenVerifier(IOptions<GoogleIdTokenVerifierOptions> options, ILogger<GoogleIdTokenVerifier> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GoogleVerifiedIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _options.Audiences,
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings).ConfigureAwait(false);

            // An email is required — it is the human-facing account identity. Treat its absence as untrusted.
            if (string.IsNullOrWhiteSpace(payload.Email))
                return null;

            var name = string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name;
            return new GoogleVerifiedIdentity(payload.Subject, payload.Email, name, payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed.");
            return null;
        }
    }
}
