namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Why an OTP operation failed.</summary>
public enum OtpFailureReason
{
    /// <summary>A code was already created for this <c>(subject, scope)</c> within the rate-limit window.</summary>
    RateLimited,

    /// <summary>The latest pending code is past its expiry.</summary>
    Expired,

    /// <summary>No pending code exists, or the supplied code doesn't match.</summary>
    InvalidCode,

    /// <summary>Too many wrong attempts against the pending code.</summary>
    MaxAttemptsReached,
}

/// <summary>Outcome of <see cref="IOtpService.CreateAsync"/>.</summary>
/// <param name="Success">Whether a code was created.</param>
/// <param name="Code">The generated code (present on success) — pass it to a delivery handler.</param>
/// <param name="FailureReason">Why creation failed (present on failure).</param>
public sealed record OtpCreationResult(bool Success, string? Code, OtpFailureReason? FailureReason)
{
    /// <summary>Successful creation carrying the generated <paramref name="code"/>.</summary>
    /// <param name="code">The generated code.</param>
    public static OtpCreationResult Succeeded(string code) => new(true, code, null);

    /// <summary>Failed creation with a <paramref name="reason"/>.</summary>
    /// <param name="reason">The failure reason.</param>
    public static OtpCreationResult Failed(OtpFailureReason reason) => new(false, null, reason);
}

/// <summary>Outcome of <see cref="IOtpService.VerifyAsync"/>.</summary>
/// <param name="Success">Whether the code matched and was consumed.</param>
/// <param name="FailureReason">Why verification failed (present on failure).</param>
public sealed record OtpVerificationResult(bool Success, OtpFailureReason? FailureReason)
{
    /// <summary>Successful verification.</summary>
    public static OtpVerificationResult Succeeded() => new(true, null);

    /// <summary>Failed verification with a <paramref name="reason"/>.</summary>
    /// <param name="reason">The failure reason.</param>
    public static OtpVerificationResult Failed(OtpFailureReason reason) => new(false, reason);
}

/// <summary>One stored OTP. The canonical shape every <see cref="IOtpStore"/> maps onto.</summary>
/// <param name="Id">Stable record id.</param>
/// <param name="Subject">Consumer-defined identity key (phone, email, username).</param>
/// <param name="Code">The code as generated.</param>
/// <param name="Scope">Consumer-defined scope the code is valid for.</param>
/// <param name="CreatedAt">Creation instant (UTC).</param>
/// <param name="ExpiresAt">Expiry instant (UTC).</param>
/// <param name="Attempts">Failed verification attempts so far.</param>
/// <param name="Consumed">Whether the code was successfully used.</param>
public sealed record OtpRecord(
    Guid Id,
    string Subject,
    string Code,
    string Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    int Attempts,
    bool Consumed);

/// <summary>What a delivery handler needs to send one code.</summary>
/// <param name="DeliveryAddress">Channel-specific recipient — Telegram chat id, phone number, email address.</param>
/// <param name="Code">The code to deliver.</param>
/// <param name="Scope">Scope the code was created for (drives message wording).</param>
/// <param name="Metadata">Optional channel-specific extras.</param>
public sealed record OtpDeliveryEnvelope(
    string DeliveryAddress,
    string Code,
    string Scope,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>Outcome of a delivery attempt.</summary>
/// <param name="Success">Whether the channel accepted the message.</param>
/// <param name="FailureReason">Channel-specific failure detail (present on failure).</param>
public sealed record OtpDeliveryResult(bool Success, string? FailureReason = null);
