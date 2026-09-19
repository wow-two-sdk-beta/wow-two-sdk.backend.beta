namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>One stored OTP. The canonical shape every <see cref="IOtpRepository"/> maps onto.</summary>
public sealed record OtpRecord
{
    /// <summary>Stable record id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Consumer-defined identity key (phone, email, username).</summary>
    public required string Subject { get; init; }

    /// <summary>The code as generated.</summary>
    public required string Code { get; init; }

    /// <summary>Consumer-defined scope the code is valid for.</summary>
    public required string Scope { get; init; }

    /// <summary>Creation instant (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Expiry instant (UTC).</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Failed verification attempts so far.</summary>
    public required int Attempts { get; init; }

    /// <summary>Whether the code was successfully used.</summary>
    public required bool Consumed { get; init; }
}
