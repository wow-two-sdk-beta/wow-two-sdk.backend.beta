using Google.Apis.Auth;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google.Authenticators;

/// <summary>Authenticates a Google identity from an ID token using Google's published keys and the configured OAuth client audiences.</summary>
/// <remarks>
/// Wraps <see cref="GoogleJsonWebSignature.ValidateAsync(string, GoogleJsonWebSignature.ValidationSettings)"/>; a token
/// with no email, a bad signature, a wrong audience, or expiry resolves to <c>null</c>. The provider API has no
/// cancellation overload, so cancellation stops awaiting it while any certificate refresh already in flight finishes
/// independently. Tests swap the seam in.
/// </remarks>
public sealed partial class GoogleIdTokenAuthenticator : IGoogleIdTokenAuthenticator
{
    private readonly GoogleIdTokenAuthenticatorOptions _options;
    private readonly ILogger<GoogleIdTokenAuthenticator> _logger;

    /// <summary>Creates the authenticator over its audience options and a logger.</summary>
    /// <param name="options">Accepted audiences (OAuth client ids).</param>
    /// <param name="logger">Logger for validation failures.</param>
    public GoogleIdTokenAuthenticator(GoogleIdTokenAuthenticatorOptions options, ILogger<GoogleIdTokenAuthenticator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GoogleVerifiedIdentity?> AuthenticateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _options.Audiences,
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            // An email is required — it is the human-facing account identity. Treat its absence as untrusted.
            if (string.IsNullOrWhiteSpace(payload.Email))
                return null;

            var name = string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name;
            return new GoogleVerifiedIdentity { Subject = payload.Subject, Email = payload.Email, Name = name, Picture = payload.Picture };
        }
        catch (InvalidJwtException ex)
        {
            LogValidationFailed(_logger, ex);
            return null;
        }
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Google ID token validation failed.")]
    private static partial void LogValidationFailed(ILogger logger, Exception exception);
}
