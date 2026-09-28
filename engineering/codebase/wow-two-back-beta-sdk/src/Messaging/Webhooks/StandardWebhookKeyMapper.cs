using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Maps a Standard Webhooks secret to its HMAC key: the base64 after <c>whsec_</c>, else the secret's UTF-8 bytes.</summary>
internal static class StandardWebhookKeyMapper
{
    private const string Prefix = "whsec_";

    /// <summary>The HMAC key of <paramref name="secret"/>.</summary>
    public static byte[] KeyOf(string secret)
    {
        var encoded = secret.StartsWith(Prefix, StringComparison.Ordinal) ? secret[Prefix.Length..] : secret;
        var key = new byte[encoded.Length];
        return Convert.TryFromBase64String(encoded, key, out var written) ? key[..written] : Encoding.UTF8.GetBytes(secret);
    }
}
