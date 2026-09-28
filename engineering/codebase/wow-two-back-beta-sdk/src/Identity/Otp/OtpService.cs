using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Provides one-time-code creation and verification.</summary>
public sealed class OtpService : IOtpService
{
    private readonly IOtpRepository _store;
    private readonly IOtpCodeGenerator _codeGenerator;
    private readonly OtpOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the service over a store, generator, options, and time source.</summary>
    /// <param name="store">Record persistence.</param>
    /// <param name="codeGenerator">Code generation strategy.</param>
    /// <param name="options">Lifetime / rate-limit / attempt settings.</param>
    /// <param name="timeProvider">Time source for creation, expiry, and rate-limit checks.</param>
    public OtpService(
        IOtpRepository store,
        IOtpCodeGenerator codeGenerator,
        OtpOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(codeGenerator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _codeGenerator = codeGenerator;
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<OtpCreationResult> CreateAsync(string subject, string scope, CancellationToken cancellationToken = default)
        => CreateAsync(subject, scope, _options.DefaultSpec, cancellationToken);

    /// <inheritdoc />
    public async Task<OtpCreationResult> CreateAsync(string subject, string scope, OtpCodeSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(spec);

        var rateLimited = await _store
            .HasRecentPendingAsync(subject, scope, _options.RateLimitWindow, cancellationToken)
            .ConfigureAwait(false);
        if (rateLimited)
        {
            return OtpCreationResult.Failed(OtpFailureReason.RateLimited);
        }

        var code = _codeGenerator.Generate(spec);
        var now = _timeProvider.GetUtcNow();
        var record = new OtpRecord
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            Code = code,
            Scope = scope,
            CreatedAt = now,
            ExpiresAt = now + (spec.Lifetime ?? _options.CodeLifetime),
            Attempts = 0,
            Consumed = false,
        };

        await _store.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        return OtpCreationResult.Succeeded(code, record.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<OtpVerificationResult> VerifyAsync(string subject, string code, string scope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (string.IsNullOrWhiteSpace(code))
        {
            return OtpVerificationResult.Failed(OtpFailureReason.InvalidCode);
        }

        var record = await _store.FindLatestPendingAsync(subject, scope, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return OtpVerificationResult.Failed(OtpFailureReason.InvalidCode);
        }

        if (_timeProvider.GetUtcNow() > record.ExpiresAt)
        {
            return OtpVerificationResult.Failed(OtpFailureReason.Expired);
        }

        if (record.Attempts >= _options.MaxAttempts)
        {
            return OtpVerificationResult.Failed(OtpFailureReason.MaxAttemptsReached);
        }

        if (!CodesMatch(record.Code, code))
        {
            await _store.IncrementAttemptsAsync(record.Id, cancellationToken).ConfigureAwait(false);
            return OtpVerificationResult.Failed(OtpFailureReason.InvalidCode);
        }

        await _store.MarkConsumedAsync(record.Id, cancellationToken).ConfigureAwait(false);
        return OtpVerificationResult.Succeeded();
    }

    /// <summary>Compares in constant time, ignoring case, spaces and dashes, so <c>ab3-k9q</c> matches <c>AB3K9Q</c>.</summary>
    private static bool CodesMatch(string expected, string provided)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Canonical(expected)), Encoding.UTF8.GetBytes(Canonical(provided)));

    private static string Canonical(string code)
        => code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
