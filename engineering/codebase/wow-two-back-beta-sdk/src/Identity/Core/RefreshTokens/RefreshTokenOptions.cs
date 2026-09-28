namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

/// <summary>Holds the refresh-token lifetime.</summary>
public sealed record RefreshTokenOptions
{
    /// <summary>How long each issued token stays redeemable; every rotation starts a new window. Default 30 days.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(30);
}
