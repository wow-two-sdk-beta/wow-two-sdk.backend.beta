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
