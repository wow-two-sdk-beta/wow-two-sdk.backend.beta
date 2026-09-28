namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>Holds the breached-password check's endpoint, threshold and failure behavior.</summary>
public sealed record BreachedPasswordOptions
{
    /// <summary>Base address of the range API. Default <c>https://api.pwnedpasswords.com/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://api.pwnedpasswords.com/");

    /// <summary>Occurrences at or above which a password is rejected. Default 1.</summary>
    public int MinimumBreachCount { get; set; } = 1;

    /// <summary>Upper bound on one range query. Default 3 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Accept the password when the corpus is unreachable, logging a warning. Default true.</summary>
    public bool FailOpen { get; set; } = true;
}
