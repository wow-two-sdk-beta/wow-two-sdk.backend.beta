using System.Globalization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Logins;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>
/// Provides the delivered-code second factors beside the authenticator: lists the methods an account can use, sends a
/// method's code through its channel and broker, verifies it, enables two-factor with it and keeps the preferred method.
/// Methods come from <see cref="TwoFactorOptions.Methods"/>; codes live in <see cref="IOtpService"/>, one scope per method.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The core user store.</param>
/// <param name="tokens">The stored-token repository, holding the preferred method.</param>
/// <param name="authenticator">The authenticator and recovery-code slice.</param>
/// <param name="otp">Creates and verifies the delivered codes.</param>
/// <param name="handlers">Creates the delivery handler per channel.</param>
/// <param name="formatter">Words each code for its channel and the recipient's culture.</param>
/// <param name="options">The methods; <c>Identity:TwoFactor</c> reloads live.</param>
/// <param name="otpOptions">The default code lifetime.</param>
/// <param name="logins">The external-login slice, for methods addressed to a login; null when absent.</param>
public sealed class UserTwoFactorMethodService<TUser, TKey>(
    IUserRepository<TUser, TKey> repository,
    IUserTokenRepository<TKey> tokens,
    UserTwoFactorService<TUser, TKey> authenticator,
    IOtpService otp,
    IOtpDeliveryHandlerFactory handlers,
    IOtpMessageFormatter formatter,
    IOptionsMonitor<TwoFactorOptions> options,
    OtpOptions otpOptions,
    IUserLoginRepository<TKey>? logins = null)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The purpose delivered codes are worded for, as <see cref="IOtpMessageFormatter"/> templates key it.</summary>
    public const string MessagePurpose = "two-factor";

    /// <summary>The methods <paramref name="user"/> can use now: the authenticator once set up, then each configured method with a registered channel and a confirmed address.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<string>> GetAvailableMethodsAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var available = new List<string>();
        if (await authenticator.GetAuthenticatorKeyAsync(user, cancellationToken) is not null)
            available.Add(TwoFactorMethodNameConstants.Authenticator);

        foreach (var (name, method) in options.CurrentValue.Methods)
        {
            if (handlers.Create(method.Channel) is not null && await AddressOfAsync(user, method, cancellationToken) is not null)
                available.Add(name);
        }

        return available;
    }

    /// <summary>The method a sign-in asks for first: the stored choice while it stays available, else the first available; null when none is.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string?> GetPreferredMethodAsync(TUser user, CancellationToken cancellationToken = default)
    {
        var available = await GetAvailableMethodsAsync(user, cancellationToken);
        var stored = await tokens.GetAsync(user.Id, UserTwoFactorService<TUser, TKey>.TokenProvider, UserTwoFactorService<TUser, TKey>.PreferredMethodName, cancellationToken);
        return available.FirstOrDefault(method => string.Equals(method, stored, StringComparison.OrdinalIgnoreCase))
            ?? (available.Count > 0 ? available[0] : null);
    }

    /// <summary>Store <paramref name="method"/> as the one a sign-in asks for first.</summary>
    /// <param name="user">The user.</param>
    /// <param name="method">An available method name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> SetPreferredMethodAsync(TUser user, string method, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        var available = await GetAvailableMethodsAsync(user, cancellationToken);
        var match = available.FirstOrDefault(name => string.Equals(name, method, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return Unavailable(method);

        await tokens.SetAsync(user.Id, UserTwoFactorService<TUser, TKey>.TokenProvider, UserTwoFactorService<TUser, TKey>.PreferredMethodName, match, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Create a code for <paramref name="method"/> and deliver it through the method's channel and broker.</summary>
    /// <param name="user">The user.</param>
    /// <param name="method">A configured method name.</param>
    /// <param name="culture">The recipient's culture for the wording; null takes the current UI culture.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<TwoFactorCodeResult> SendCodeAsync(TUser user, string method, CultureInfo? culture = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (string.Equals(method, TwoFactorMethodNameConstants.Authenticator, StringComparison.OrdinalIgnoreCase))
            return new TwoFactorCodeResult { Status = TwoFactorCodeStatus.NotDeliverable, Method = TwoFactorMethodNameConstants.Authenticator };

        if (!options.CurrentValue.Methods.TryGetValue(method, out var settings)
            || handlers.Create(settings.Channel) is not { } handler
            || await AddressOfAsync(user, settings, cancellationToken) is not { } address)
            return new TwoFactorCodeResult { Status = TwoFactorCodeStatus.Unavailable, Method = method };

        var lifetime = settings.Code.Lifetime ?? otpOptions.CodeLifetime;
        var created = await otp.CreateAsync(SubjectOf(user), ScopeOf(method), settings.Code with { Lifetime = lifetime }, cancellationToken);
        if (!created.Success)
            return new TwoFactorCodeResult { Status = TwoFactorCodeStatus.RateLimited, Method = method };

        culture ??= CultureInfo.CurrentUICulture;
        var worded = formatter.Format(MessagePurpose, settings.Channel, created.Code!, lifetime, culture);
        var delivered = await handler.SendAsync(
            new OtpDeliveryEnvelopeModel
            {
                DeliveryAddress = address,
                Code = created.Code!,
                Scope = MessagePurpose,
                Text = worded.Text,
                Subject = worded.Subject,
                Broker = settings.Broker,
                Lifetime = lifetime,
                Culture = culture.Name,
            },
            cancellationToken);

        return delivered.Success
            ? new TwoFactorCodeResult { Status = TwoFactorCodeStatus.Sent, Method = method, ExpiresAt = created.ExpiresAt }
            : new TwoFactorCodeResult { Status = TwoFactorCodeStatus.DeliveryFailed, Method = method, FailureReason = delivered.FailureReason };
    }

    /// <summary>Whether <paramref name="code"/> verifies for <paramref name="method"/>; a delivered code is spent on success.</summary>
    /// <param name="user">The user.</param>
    /// <param name="method">The method name, or <see cref="TwoFactorMethodNameConstants.Authenticator"/>.</param>
    /// <param name="code">The code the user entered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> VerifyCodeAsync(TUser user, string method, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (string.Equals(method, TwoFactorMethodNameConstants.Authenticator, StringComparison.OrdinalIgnoreCase))
            return await authenticator.VerifyAuthenticatorCodeAsync(user, code, cancellationToken);

        return options.CurrentValue.Methods.ContainsKey(method)
            && !string.IsNullOrWhiteSpace(code)
            && (await otp.VerifyAsync(SubjectOf(user), code, ScopeOf(method), cancellationToken)).Success;
    }

    /// <summary>
    /// Enable two-factor with <paramref name="method"/> once <paramref name="code"/> proves the user receives it, and make
    /// it the preferred method; rotates the stamp. The authenticator enables through its own key check.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="method">An available method name.</param>
    /// <param name="code">The code the method delivered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> EnableAsync(TUser user, string method, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (string.Equals(method, TwoFactorMethodNameConstants.Authenticator, StringComparison.OrdinalIgnoreCase))
        {
            var enabled = await authenticator.EnableAsync(user, code, cancellationToken);
            return enabled.Succeeded ? await SetPreferredMethodAsync(user, TwoFactorMethodNameConstants.Authenticator, cancellationToken) : enabled;
        }

        if (!(await GetAvailableMethodsAsync(user, cancellationToken)).Contains(method, StringComparer.OrdinalIgnoreCase))
            return Unavailable(method);
        if (!await VerifyCodeAsync(user, method, code, cancellationToken))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.InvalidTwoFactorCode, "The code is invalid or has expired.");

        user.TwoFactorEnabled = true;
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return await SetPreferredMethodAsync(user, method, cancellationToken);
    }

    private async Task<string?> AddressOfAsync(TUser user, TwoFactorMethodOptions method, CancellationToken cancellationToken)
    {
        switch (method.AddressKind)
        {
            case TwoFactorAddressKind.PhoneNumber:
                return user.PhoneNumberConfirmed && !string.IsNullOrWhiteSpace(user.PhoneNumber) ? user.PhoneNumber : null;
            case TwoFactorAddressKind.Email:
                return user.EmailConfirmed && !string.IsNullOrWhiteSpace(user.Email) ? user.Email : null;
            case TwoFactorAddressKind.ExternalLogin when logins is not null:
                var linked = await logins.GetAsync(user.Id, cancellationToken);
                return linked.FirstOrDefault(login => string.Equals(login.LoginProvider, method.LoginProvider, StringComparison.OrdinalIgnoreCase))?.ProviderKey;
            default:
                return null;
        }
    }

    private static IdentityResult Unavailable(string method)
        => IdentityUserExtensions.Failure(
            IdentityErrorCodeConstants.TwoFactorMethodUnavailable,
            $"The two-factor method '{method}' is not available for this account.",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["Method"] = method });

    private static string SubjectOf(TUser user) => Convert.ToString(user.Id, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string ScopeOf(string method) => "identity.two-factor." + method.ToLowerInvariant();
}
