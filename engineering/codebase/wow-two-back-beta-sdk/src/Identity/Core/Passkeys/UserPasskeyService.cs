using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;

/// <summary>
/// Provides the passkey slice over Fido2: register a WebAuthn credential for a user, sign in with one — usernameless, as
/// passkeys are discoverable — and list or remove them. Ceremony state travels sealed by ASP.NET Data Protection, so any
/// host completes a ceremony another started, until it expires. A sign-in ceremony completes at most once per passkey:
/// a use is recorded only when the passkey's last use predates the ceremony, which also defeats replays from
/// authenticators that keep no signature counter.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="users">The core user store.</param>
/// <param name="passkeys">The passkey store.</param>
/// <param name="protection">Seals ceremony state.</param>
/// <param name="options">The relying party and ceremony settings; <c>Identity:Passkeys</c> reloads live.</param>
/// <param name="time">The clock registrations and uses are stamped with.</param>
public sealed class UserPasskeyService<TUser, TKey>(
    IUserRepository<TUser, TKey> users,
    IUserPasskeyRepository<TKey> passkeys,
    IDataProtectionProvider protection,
    IOptionsMonitor<PasskeyOptions> options,
    TimeProvider time)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private const string Registration = "register";
    private const string SignIn = "sign-in";
    private readonly IDataProtector _protector = protection.CreateProtector("WoW2.Identity.Passkeys.v1");

    /// <summary>Starts registering a passkey for <paramref name="user"/>; the user's existing passkeys are excluded.</summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<PasskeyCeremonyModel> BeginRegistrationAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var existing = await passkeys.ListAsync(user.Id, cancellationToken);
        var label = user.UserName ?? user.Email ?? IdOf(user);
        var request = Fido().RequestNewCredential(
            new Fido2User { Id = UserHandle(user), Name = label, DisplayName = label },
            [.. existing.Select(passkey => new PublicKeyCredentialDescriptor(passkey.CredentialId))],
            new AuthenticatorSelection { RequireResidentKey = true, UserVerification = Verification },
            AttestationConveyancePreference.None,
            null);
        var json = request.ToJson();
        return new PasskeyCeremonyModel { OptionsJson = json, State = Seal(Registration, IdOf(user), json) };
    }

    /// <summary>Completes a registration with the credential the browser created.</summary>
    /// <param name="user">The signed-in user who started it.</param>
    /// <param name="state">The state from <see cref="BeginRegistrationAsync"/>.</param>
    /// <param name="credentialJson">The <c>PublicKeyCredential</c> JSON from <c>navigator.credentials.create</c>.</param>
    /// <param name="name">A label for the passkey, such as the device; null names it "Passkey".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> CompleteRegistrationAsync(TUser user, string state, string credentialJson, string? name = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (Open(state, Registration) is not { } ceremony || ceremony.Subject != IdOf(user) || Parse<AuthenticatorAttestationRawResponse>(credentialJson) is not { } response)
            return Invalid("The passkey registration expired or does not belong to this account.");

        try
        {
            var made = await Fido().MakeNewCredentialAsync(
                response,
                CredentialCreateOptions.FromJson(ceremony.Options),
                async (candidate, token) => await passkeys.FindAsync(candidate.CredentialId, token) is null,
                null,
                cancellationToken);
            var credential = made.Result ?? throw new Fido2VerificationException(made.ErrorMessage ?? "The attestation did not verify.");
            await passkeys.AddAsync(
                new IdentityPasskey<TKey>
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CredentialId = credential.CredentialId,
                    PublicKey = credential.PublicKey,
                    UserHandle = credential.User.Id,
                    SignCount = credential.Counter,
                    AaGuid = credential.Aaguid,
                    Name = string.IsNullOrWhiteSpace(name) ? "Passkey" : name.Trim()[..Math.Min(name.Trim().Length, 100)],
                    CreatedAt = time.GetUtcNow(),
                },
                cancellationToken);
            return IdentityResult.Success;
        }
        catch (Fido2VerificationException exception)
        {
            return Invalid(exception.Message);
        }
    }

    /// <summary>Starts a usernameless sign-in: the browser offers every passkey it holds for this site.</summary>
    public PasskeyCeremonyModel BeginSignIn()
    {
        var json = Fido().GetAssertionOptions([], Verification, null).ToJson();
        return new PasskeyCeremonyModel { OptionsJson = json, State = Seal(SignIn, string.Empty, json) };
    }

    /// <summary>
    /// Verifies the assertion the browser signed and returns its user, or null when the state expired, the credential is
    /// unknown or the signature fails; the counter and last use are recorded. Complete the sign-in with
    /// <c>SignInService.PasskeySignInAsync</c>, which calls this.
    /// </summary>
    /// <param name="state">The state from <see cref="BeginSignIn"/>.</param>
    /// <param name="credentialJson">The <c>PublicKeyCredential</c> JSON from <c>navigator.credentials.get</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<TUser?> VerifySignInAsync(string state, string credentialJson, CancellationToken cancellationToken = default)
    {
        if (Open(state, SignIn) is not { } ceremony
            || Parse<AuthenticatorAssertionRawResponse>(credentialJson) is not { } response
            || await passkeys.FindAsync(response.RawId ?? response.Id, cancellationToken) is not { } passkey)
            return null;

        try
        {
            var verified = await Fido().MakeAssertionAsync(
                response,
                AssertionOptions.FromJson(ceremony.Options),
                passkey.PublicKey,
                passkey.SignCount,
                (owner, _) => Task.FromResult(owner.UserHandle is null || owner.UserHandle.AsSpan().SequenceEqual(passkey.UserHandle)),
                null,
                cancellationToken);
            var now = time.GetUtcNow();
            return await passkeys.RecordUseAsync(passkey.Id, verified.Counter, ceremony.IssuedAt, now > ceremony.IssuedAt ? now : ceremony.IssuedAt, cancellationToken)
                ? await users.FindByIdAsync(passkey.UserId, cancellationToken)
                : null;
        }
        catch (Fido2VerificationException)
        {
            return null;
        }
    }

    /// <summary>The user's passkeys, without key material.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<PasskeyModel>> ListAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return [.. (await passkeys.ListAsync(user.Id, cancellationToken)).Select(passkey => new PasskeyModel
        {
            Id = passkey.Id,
            Name = passkey.Name,
            CreatedAt = passkey.CreatedAt,
            LastUsedAt = passkey.LastUsedAt,
        })];
    }

    /// <summary>Removes one of the user's passkeys; false when the user has no such passkey.</summary>
    /// <param name="user">The user.</param>
    /// <param name="id">The passkey.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RemoveAsync(TUser user, Guid id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return passkeys.RemoveAsync(user.Id, id, cancellationToken);
    }

    private UserVerificationRequirement Verification
        => options.CurrentValue.RequireUserVerification ? UserVerificationRequirement.Required : UserVerificationRequirement.Preferred;

    private Fido2 Fido()
    {
        var current = options.CurrentValue;
        return new Fido2(new Fido2Configuration
        {
            ServerDomain = current.ServerDomain,
            ServerName = current.ServerName,
            Origins = [.. current.Origins],
            TimestampDriftTolerance = 300_000,
        });
    }

    /// <summary>Seals the ceremony with its start, whole milliseconds so every store compares it exactly.</summary>
    private string Seal(string kind, string subject, string optionsJson)
        => _protector.Protect(string.Join('\n', kind, subject, time.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), optionsJson));

    /// <summary>Opens a ceremony of <paramref name="kind"/> that is still within the configured lifetime.</summary>
    private Ceremony? Open(string state, string kind)
    {
        if (string.IsNullOrWhiteSpace(state))
            return null;

        string[] parts;
        try
        {
            parts = _protector.Unprotect(state).Split('\n', 4);
        }
        catch (CryptographicException)
        {
            return null;
        }

        if (parts.Length != 4 || parts[0] != kind || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var issued))
            return null;

        var issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(issued);
        return time.GetUtcNow() < issuedAt + options.CurrentValue.CeremonyLifetime
            ? new Ceremony { Subject = parts[1], IssuedAt = issuedAt, Options = parts[3] }
            : null;
    }

    private static T? Parse<T>(string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Represents an opened ceremony.</summary>
    private sealed record Ceremony
    {
        public required string Subject { get; init; }

        public required DateTimeOffset IssuedAt { get; init; }

        public required string Options { get; init; }
    }

    private static IdentityResult Invalid(string description) => IdentityUserExtensions.Failure(IdentityErrorCodeConstants.InvalidPasskey, description);

    private static byte[] UserHandle(TUser user) => SHA256.HashData(Encoding.UTF8.GetBytes("wow2-passkey:" + IdOf(user)));

    private static string IdOf(TUser user) => Convert.ToString(user.Id, CultureInfo.InvariantCulture) ?? string.Empty;
}
