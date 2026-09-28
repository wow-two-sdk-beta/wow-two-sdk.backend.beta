using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>
/// Calls the Pwned Passwords k-anonymity range API: sends the first five hex characters of the password's SHA-1 and
/// matches the returned suffixes locally. Requests padded responses so the response size reveals nothing either.
/// </summary>
/// <param name="http">The typed client; its base address points at the range API.</param>
public sealed class PwnedPasswordsClient(HttpClient http) : IPwnedPasswordsClient
{
    private const int PrefixLength = 5;
    private const int SuffixLength = 35;

    /// <inheritdoc />
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "The range API is keyed by SHA-1; the digest protects nothing.")]
    public async Task<long> GetBreachCountAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);

        var digest = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        var suffix = digest.AsMemory(PrefixLength);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{digest[..PrefixLength]}");
        request.Headers.Add("Add-Padding", "true");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length <= SuffixLength + 1 || line[SuffixLength] != ':')
                continue;
            if (!line.AsSpan(0, SuffixLength).Equals(suffix.Span, StringComparison.OrdinalIgnoreCase))
                continue;

            return long.TryParse(line.AsSpan(SuffixLength + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
        }

        return 0;
    }
}
