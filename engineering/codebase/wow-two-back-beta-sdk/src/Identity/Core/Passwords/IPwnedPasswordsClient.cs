namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>Defines the Pwned Passwords range query: how often a password appears in known breaches.</summary>
public interface IPwnedPasswordsClient
{
    /// <summary>Counts the breach occurrences of <paramref name="password"/>; only a five-character hash prefix leaves the host.</summary>
    /// <param name="password">The candidate password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The occurrence count; zero when the corpus does not contain the password.</returns>
    Task<long> GetBreachCountAsync(string password, CancellationToken cancellationToken = default);
}
