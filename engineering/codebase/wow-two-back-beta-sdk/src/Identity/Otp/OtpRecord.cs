namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>One stored OTP. The canonical shape every <see cref="IOtpRepository"/> maps onto.</summary>
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
