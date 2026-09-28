using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Emails;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;
using WoW.Two.Sdk.Backend.Beta.Web.Contracts;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Maps the account HTTP API over the registered identity slices.</summary>
public static class UserAccountEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>register</c>, <c>login</c>, <c>refresh</c>, <c>logout</c>, <c>confirm-email</c>,
    /// <c>resend-confirmation-email</c>, <c>forgot-password</c>, <c>reset-password</c>, <c>manage/info</c> and, with the
    /// two-factor slice registered, <c>manage/2fa</c> (status, authenticator, enable, disable, recovery-codes, preferred,
    /// methods/{method}/send, methods/{method}/enable) under
    /// <paramref name="endpoints"/> — mount it on a group such as <c>app.MapGroup("/account")</c>. Sign-in returns a bearer
    /// session (JWT via <see cref="ITokenIssuer"/>, plus a refresh token when registered) or, with <c>?useCookies=true</c>,
    /// a cookie. Failures surface as problem details, translated when error translation is enabled.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="endpoints">The route builder or group.</param>
    public static IEndpointRouteBuilder MapUserAccountEndpoints<TUser, TKey>(this IEndpointRouteBuilder endpoints)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/register", RegisterAsync<TUser, TKey>);
        endpoints.MapPost("/login", SignInAsync<TUser, TKey>);
        endpoints.MapPost("/refresh", RefreshAsync<TUser, TKey>);
        endpoints.MapPost("/logout", SignOutAsync<TUser, TKey>);
        endpoints.MapGet("/confirm-email", ConfirmEmailAsync<TUser, TKey>);
        endpoints.MapPost("/resend-confirmation-email", ResendConfirmationAsync<TUser, TKey>);
        endpoints.MapPost("/forgot-password", ForgotPasswordAsync<TUser, TKey>);
        endpoints.MapPost("/reset-password", ResetPasswordAsync<TUser, TKey>);
        endpoints.MapGet("/manage/info", InfoAsync<TUser, TKey>).RequireAuthorization();

        if (endpoints.ServiceProvider.GetService<IServiceProviderIsService>()?.IsService(typeof(UserTwoFactorService<TUser, TKey>)) == true)
        {
            endpoints.MapGet("/manage/2fa", TwoFactorStatusAsync<TUser, TKey>).RequireAuthorization();
            endpoints.MapPost("/manage/2fa/authenticator", ResetAuthenticatorAsync<TUser, TKey>).RequireAuthorization();
            endpoints.MapPost("/manage/2fa/enable", EnableTwoFactorAsync<TUser, TKey>).RequireAuthorization();
            endpoints.MapPost("/manage/2fa/disable", DisableTwoFactorAsync<TUser, TKey>).RequireAuthorization();
            endpoints.MapPost("/manage/2fa/recovery-codes", RegenerateRecoveryCodesAsync<TUser, TKey>).RequireAuthorization();
            endpoints.MapPost("/manage/2fa/preferred", SetPreferredMethodAsync<TUser, TKey>).RequireAuthorization();
            endpoints.MapPost("/manage/2fa/methods/{method}/send", SendMethodCodeAsync<TUser, TKey>).RequireAuthorization();
            endpoints.MapPost("/manage/2fa/methods/{method}/enable", EnableMethodAsync<TUser, TKey>).RequireAuthorization();
        }

        return endpoints;
    }

    /// <summary>Maps the account API for a <see cref="Guid"/>-keyed user.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <param name="endpoints">The route builder or group.</param>
    public static IEndpointRouteBuilder MapUserAccountEndpoints<TUser>(this IEndpointRouteBuilder endpoints)
        where TUser : IdentityUser<Guid>, new()
        => endpoints.MapUserAccountEndpoints<TUser, Guid>();

    private static async Task<IResult> RegisterAsync<TUser, TKey>(RegisterAccountApiRequest request, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var user = new TUser { UserName = string.IsNullOrWhiteSpace(request.UserName) ? request.Email : request.UserName, Email = request.Email };
        var created = await services.GetRequiredService<UserPasswordService<TUser, TKey>>().CreateWithPasswordAsync(user, request.Password, cancellationToken);
        if (!created.Succeeded)
            throw created.ToValidationError().ToException();

        var sent = await SendConfirmationAsync<TUser, TKey>(services, user, cancellationToken);
        return Results.Ok(ApiResponse<RegisteredAccountDto>.Ok(new RegisteredAccountDto { UserId = IdOf(user), ConfirmationSent = sent }));
    }

    private static async Task<IResult> SignInAsync<TUser, TKey>(SignInApiRequest request, bool? useCookies, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var signIn = services.GetRequiredService<SignInService<TUser, TKey>>();
        var result = await signIn.PasswordSignInAsync(request.Login, request.Password, cancellationToken);
        if (result is { Status: SignInStatus.RequiresTwoFactor, TwoFactorTicket: { } ticket } && TryParseKey<TKey>(result.UserId, out var userId))
        {
            var method = request.TwoFactorMethod ?? result.TwoFactorMethod ?? TwoFactorMethodNameConstants.Authenticator;
            if (!string.IsNullOrWhiteSpace(request.TwoFactorCode))
                result = await signIn.TwoFactorSignInAsync(userId, ticket, method, request.TwoFactorCode, cancellationToken);
            else if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
                result = await signIn.RecoveryCodeSignInAsync(userId, ticket, request.RecoveryCode, cancellationToken);
            else
                throw (await TwoFactorChallengeAsync<TUser, TKey>(services, signIn, userId, ticket, method, request.TwoFactorMethod is not null, cancellationToken)).ToException();
        }

        if (!result.Succeeded)
            throw FailureOf(result.Status).ToException();

        if (useCookies == true)
        {
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal!);
            return Results.NoContent();
        }

        var user = await FindAsync<TUser, TKey>(services, result.UserId, cancellationToken);
        return Results.Ok(ApiResponse<AccessTokenDto>.Ok(await IssueAsync<TUser, TKey>(services, result.Principal!, user, null, cancellationToken)));
    }

    private static async Task<IResult> RefreshAsync<TUser, TKey>(RefreshAccessApiRequest request, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var refresh = services.GetService<RefreshTokenService<TUser, TKey>>()
            ?? throw new NotSupportedException("Refreshing needs the refresh-token slice; call .AddRefreshTokens().");
        var redeemed = await refresh.RedeemAsync(request.RefreshToken, cancellationToken);
        if (!redeemed.Succeeded)
            throw Unauthorized(IdentityErrorCodeConstants.InvalidRefreshToken, "The session has expired; sign in again.").ToException();

        var renewed = await services.GetRequiredService<SignInService<TUser, TKey>>().RenewAsync(redeemed.User!, cancellationToken);
        if (!renewed.Succeeded)
        {
            await refresh.RevokeAsync(redeemed.Token!.Token, cancellationToken);
            throw FailureOf(renewed.Status).ToException();
        }

        return Results.Ok(ApiResponse<AccessTokenDto>.Ok(await IssueAsync<TUser, TKey>(services, renewed.Principal!, redeemed.User, redeemed.Token!.Token, cancellationToken)));
    }

    private static async Task<IResult> SignOutAsync<TUser, TKey>(SignOutApiRequest? request, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken) && http.RequestServices.GetService<RefreshTokenService<TUser, TKey>>() is { } refresh)
            await refresh.RevokeAsync(request.RefreshToken, cancellationToken);

        if (await http.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme) is not null)
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmailAsync<TUser, TKey>(string userId, string token, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var user = await FindAsync<TUser, TKey>(services, userId, cancellationToken);
        var confirmed = user is null
            ? IdentityResult.Failed(new IdentityError { Code = IdentityErrorCodeConstants.InvalidToken, Description = "The link is invalid or has expired." })
            : await services.GetRequiredService<UserEmailService<TUser, TKey>>().ConfirmEmailAsync(user, token, cancellationToken);
        if (!confirmed.Succeeded)
            throw confirmed.ToValidationError().ToException();

        return Results.NoContent();
    }

    private static async Task<IResult> ResendConfirmationAsync<TUser, TKey>(EmailAddressApiRequest request, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var user = await services.GetRequiredService<UserAccountService<TUser, TKey>>().FindByEmailAsync(request.Email, cancellationToken);
        if (user is { EmailConfirmed: false })
            await SendConfirmationAsync<TUser, TKey>(services, user, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync<TUser, TKey>(EmailAddressApiRequest request, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var user = await services.GetRequiredService<UserAccountService<TUser, TKey>>().FindByEmailAsync(request.Email, cancellationToken);
        if (user is not null && services.GetService<UserAccountMailService<TUser, TKey>>() is { } mail)
        {
            var token = services.GetRequiredService<UserPasswordService<TUser, TKey>>().IssuePasswordResetToken(user);
            await mail.SendPasswordResetAsync(user, token, cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync<TUser, TKey>(ResetPasswordApiRequest request, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var user = await services.GetRequiredService<UserAccountService<TUser, TKey>>().FindByEmailAsync(request.Email, cancellationToken);
        var reset = user is null
            ? IdentityResult.Failed(new IdentityError { Code = IdentityErrorCodeConstants.InvalidToken, Description = "The reset link is invalid or has expired." })
            : await services.GetRequiredService<UserPasswordService<TUser, TKey>>().ResetPasswordAsync(user, request.ResetToken, request.NewPassword, cancellationToken);
        if (!reset.Succeeded)
            throw reset.ToValidationError().ToException();

        return Results.NoContent();
    }

    private static async Task<IResult> InfoAsync<TUser, TKey>(ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var claimType = services.GetRequiredService<IdentityCoreOptions>().Claims.UserIdClaimType;
        var user = await FindAsync<TUser, TKey>(services, principal.FindFirst(claimType)?.Value, cancellationToken)
            ?? throw AppErrorFactory.Unauthorized("The account no longer exists.").ToException();

        return Results.Ok(ApiResponse<AccountInfoDto>.Ok(new AccountInfoDto
        {
            UserId = IdOf(user),
            UserName = user.UserName,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
        }));
    }

    private static async Task<IResult> TwoFactorStatusAsync<TUser, TKey>(ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, twoFactor) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        var methods = http.RequestServices.GetService<UserTwoFactorMethodService<TUser, TKey>>();
        return Results.Ok(ApiResponse<TwoFactorStatusDto>.Ok(new TwoFactorStatusDto
        {
            Enabled = user.TwoFactorEnabled,
            HasAuthenticator = await twoFactor.GetAuthenticatorKeyAsync(user, cancellationToken) is not null,
            RecoveryCodesLeft = await twoFactor.CountRecoveryCodesAsync(user, cancellationToken),
            Methods = methods is null ? [] : await methods.GetAvailableMethodsAsync(user, cancellationToken),
            PreferredMethod = methods is null ? null : await methods.GetPreferredMethodAsync(user, cancellationToken),
        }));
    }

    private static async Task<IResult> ResetAuthenticatorAsync<TUser, TKey>(ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, twoFactor) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        var key = await twoFactor.ResetAuthenticatorKeyAsync(user, cancellationToken);
        var uri = await twoFactor.GetAuthenticatorUriAsync(user, cancellationToken);
        return Results.Ok(ApiResponse<AuthenticatorSetupDto>.Ok(new AuthenticatorSetupDto { SharedKey = key, AuthenticatorUri = uri!.ToString() }));
    }

    private static async Task<IResult> EnableTwoFactorAsync<TUser, TKey>(TwoFactorCodeApiRequest request, ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, twoFactor) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        var enabled = await twoFactor.EnableAsync(user, request.Code, cancellationToken);
        if (!enabled.Succeeded)
            throw enabled.ToValidationError().ToException();

        var codes = await twoFactor.GenerateRecoveryCodesAsync(user, cancellationToken);
        return Results.Ok(ApiResponse<RecoveryCodesDto>.Ok(new RecoveryCodesDto { RecoveryCodes = codes }));
    }

    private static async Task<IResult> DisableTwoFactorAsync<TUser, TKey>(ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, twoFactor) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        await twoFactor.DisableAsync(user, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync<TUser, TKey>(ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, twoFactor) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        if (!user.TwoFactorEnabled)
            throw AppError.Of(AppErrorType.Conflict, "Enable two-factor before generating recovery codes.", MessageKey(IdentityErrorCodeConstants.AuthenticatorNotConfigured)).ToException();

        var codes = await twoFactor.GenerateRecoveryCodesAsync(user, cancellationToken);
        return Results.Ok(ApiResponse<RecoveryCodesDto>.Ok(new RecoveryCodesDto { RecoveryCodes = codes }));
    }

    private static async Task<IResult> SetPreferredMethodAsync<TUser, TKey>(TwoFactorMethodApiRequest request, ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, _) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        var chosen = await http.RequestServices.GetRequiredService<UserTwoFactorMethodService<TUser, TKey>>().SetPreferredMethodAsync(user, request.Method, cancellationToken);
        if (!chosen.Succeeded)
            throw chosen.ToValidationError().ToException();

        return Results.NoContent();
    }

    private static async Task<IResult> SendMethodCodeAsync<TUser, TKey>(string method, ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, _) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        var sent = await http.RequestServices.GetRequiredService<UserTwoFactorMethodService<TUser, TKey>>().SendCodeAsync(user, method, cancellationToken: cancellationToken);
        return sent.Sent
            ? Results.Ok(ApiResponse<TwoFactorCodeSentDto>.Ok(new TwoFactorCodeSentDto { Method = sent.Method, ExpiresAt = sent.ExpiresAt }))
            : throw FailureOf(sent).ToException();
    }

    private static async Task<IResult> EnableMethodAsync<TUser, TKey>(string method, TwoFactorCodeApiRequest request, ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var (user, twoFactor) = await TwoFactorOfAsync<TUser, TKey>(principal, http, cancellationToken);
        var enabled = await http.RequestServices.GetRequiredService<UserTwoFactorMethodService<TUser, TKey>>().EnableAsync(user, method, request.Code, cancellationToken);
        if (!enabled.Succeeded)
            throw enabled.ToValidationError().ToException();

        var codes = await twoFactor.GenerateRecoveryCodesAsync(user, cancellationToken);
        return Results.Ok(ApiResponse<RecoveryCodesDto>.Ok(new RecoveryCodesDto { RecoveryCodes = codes }));
    }

    /// <summary>
    /// The 401 a second-factor step answers with: names the method asked for and the available ones, and sends a
    /// delivered method's code when the client chose it or <see cref="TwoFactorOptions.AutoSendCode"/> is on.
    /// </summary>
    private static async Task<AppError> TwoFactorChallengeAsync<TUser, TKey>(
        IServiceProvider services,
        SignInService<TUser, TKey> signIn,
        TKey userId,
        string ticket,
        string method,
        bool chosen,
        CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["twoFactorMethod"] = method };
        if (services.GetService<UserTwoFactorMethodService<TUser, TKey>>() is { } methods
            && await services.GetRequiredService<UserAccountService<TUser, TKey>>().FindByIdAsync(userId, cancellationToken) is { } user)
        {
            extensions["twoFactorMethods"] = await methods.GetAvailableMethodsAsync(user, cancellationToken);
            var autoSend = services.GetRequiredService<TwoFactorOptions>().AutoSendCode;
            if (!string.Equals(method, TwoFactorMethodNameConstants.Authenticator, StringComparison.OrdinalIgnoreCase) && (chosen || autoSend))
            {
                var sent = await signIn.SendTwoFactorCodeAsync(userId, ticket, method, cancellationToken: cancellationToken);
                if (sent.Status is not TwoFactorCodeStatus.Sent and not TwoFactorCodeStatus.RateLimited)
                    return FailureOf(sent);

                extensions["codeSent"] = sent.Sent;
                extensions["codeExpiresAt"] = sent.ExpiresAt;
            }
        }

        var metadata = MessageKey(IdentityErrorCodeConstants.TwoFactorRequired);
        metadata[AppErrorProblemDetailsFactory.ExtensionsMetadataKey] = extensions;
        return AppError.Of(AppErrorType.Unauthorized, "Enter your two-factor code.", metadata);
    }

    private static AppError FailureOf(TwoFactorCodeResult result) => result.Status switch
    {
        TwoFactorCodeStatus.RateLimited => AppError.Of(AppErrorType.TooManyRequests, "A code was sent moments ago; wait before asking again.", MessageKey(IdentityErrorCodeConstants.TwoFactorRequired)),
        TwoFactorCodeStatus.DeliveryFailed => AppError.Of(AppErrorType.ExternalUnavailable, "The code could not be delivered; try another method.", MessageKey(IdentityErrorCodeConstants.TwoFactorMethodUnavailable)),
        _ => AppError.Of(AppErrorType.Validation, $"The two-factor method '{result.Method}' is not available for this account.", MessageKey(IdentityErrorCodeConstants.TwoFactorMethodUnavailable)),
    };

    private static async Task<(TUser User, UserTwoFactorService<TUser, TKey> TwoFactor)> TwoFactorOfAsync<TUser, TKey>(ClaimsPrincipal principal, HttpContext http, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var services = http.RequestServices;
        var claimType = services.GetRequiredService<IdentityCoreOptions>().Claims.UserIdClaimType;
        var user = await FindAsync<TUser, TKey>(services, principal.FindFirst(claimType)?.Value, cancellationToken)
            ?? throw AppErrorFactory.Unauthorized("The account no longer exists.").ToException();
        return (user, services.GetRequiredService<UserTwoFactorService<TUser, TKey>>());
    }

    private static async Task<AccessTokenDto> IssueAsync<TUser, TKey>(IServiceProvider services, ClaimsPrincipal principal, TUser? user, string? refreshToken, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
    {
        var issuer = services.GetService<ITokenIssuer>()
            ?? throw new NotSupportedException("Bearer sign-in needs JWT issuance; call AddJwtTokenIssuance(...) or sign in with ?useCookies=true.");
        var lifetime = services.GetService<JwtTokenIssuerOptions>()?.Lifetime ?? TimeSpan.FromHours(1);
        if (refreshToken is null && user is not null && services.GetService<RefreshTokenService<TUser, TKey>>() is { } refresh)
            refreshToken = (await refresh.IssueAsync(user, cancellationToken)).Token;

        return new AccessTokenDto
        {
            AccessToken = issuer.Issue(principal.Claims, new TokenIssuanceContext { Lifetime = lifetime }),
            ExpiresIn = (int)lifetime.TotalSeconds,
            RefreshToken = refreshToken,
        };
    }

    private static async Task<bool> SendConfirmationAsync<TUser, TKey>(IServiceProvider services, TUser user, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
        => services.GetService<UserEmailService<TUser, TKey>>() is { } emails
            && services.GetService<UserAccountMailService<TUser, TKey>>() is { } mail
            && await mail.SendConfirmationAsync(user, emails.IssueConfirmationToken(user), cancellationToken);

    private static async Task<TUser?> FindAsync<TUser, TKey>(IServiceProvider services, string? id, CancellationToken cancellationToken)
        where TUser : IdentityUser<TKey>, new()
        where TKey : notnull, IEquatable<TKey>
        => TryParseKey<TKey>(id, out var key)
            ? await services.GetRequiredService<UserAccountService<TUser, TKey>>().FindByIdAsync(key, cancellationToken)
            : null;

    private static AppError FailureOf(SignInStatus status) => status switch
    {
        SignInStatus.LockedOut => AppError.Of(AppErrorType.TooManyRequests, "The account is temporarily locked out.", MessageKey(IdentityErrorCodeConstants.LockedOut)),
        SignInStatus.NotAllowed => AppError.Of(AppErrorType.Forbidden, "Confirm your email or phone to sign in.", MessageKey(IdentityErrorCodeConstants.SignInNotAllowed)),
        SignInStatus.RequiresTwoFactor => Unauthorized(IdentityErrorCodeConstants.TwoFactorRequired, "Enter your two-factor code."),
        _ => Unauthorized(IdentityErrorCodeConstants.SignInFailed, "The login or password is incorrect."),
    };

    private static AppError Unauthorized(string code, string message) => AppError.Of(AppErrorType.Unauthorized, message, MessageKey(code));

    private static Dictionary<string, object?> MessageKey(string code)
        => new(StringComparer.Ordinal) { ["messageKey"] = code, ["identityCode"] = code };

    private static bool TryParseKey<TKey>(string? value, out TKey key)
        where TKey : notnull
    {
        key = default!;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            key = (TKey)TypeDescriptor.GetConverter(typeof(TKey)).ConvertFromInvariantString(value)!;
            return key is not null;
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    private static string IdOf<TKey>(IdentityUser<TKey> user)
        where TKey : notnull, IEquatable<TKey>
        => Convert.ToString(user.Id, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}
