namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

/// <summary>Holds the purpose-token key and lifetimes.</summary>
public sealed record UserTokenOptions
{
    /// <summary>Base64 HMAC-SHA256 key, at least 32 bytes decoded; every host verifying a token needs the same key.</summary>
    public string? SigningKey { get; set; }

    /// <summary>Lifetime of a purpose without an entry in <see cref="Lifetimes"/>. Default 1 day.</summary>
    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Lifetimes by bare purpose; a scoped purpose uses the part before its separator. Defaults: confirmation 3 days, reset 2 hours, email change 1 day.</summary>
    public Dictionary<string, TimeSpan> Lifetimes { get; } = new(StringComparer.Ordinal)
    {
        [UserTokenPurposeConstants.EmailConfirmation] = TimeSpan.FromDays(3),
        [UserTokenPurposeConstants.PasswordReset] = TimeSpan.FromHours(2),
        [UserTokenPurposeConstants.ChangeEmail] = TimeSpan.FromDays(1),
    };
}
