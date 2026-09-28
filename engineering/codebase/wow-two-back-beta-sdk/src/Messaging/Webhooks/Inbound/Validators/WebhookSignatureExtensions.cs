using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Holds the HMAC, constant-time comparison and header helpers the built-in validators share.</summary>
internal static class WebhookSignatureExtensions
{
    /// <summary>The HMAC-SHA256 of <paramref name="content"/> keyed by <paramref name="key"/>.</summary>
    public static byte[] HmacSha256(this byte[] key, ReadOnlySpan<byte> content) => HMACSHA256.HashData(key, content);

    /// <summary>The UTF-8 bytes of a secret, the key most providers sign with.</summary>
    public static byte[] Utf8(this string secret) => Encoding.UTF8.GetBytes(secret);

    /// <summary><paramref name="prefix"/>, then the body, then <paramref name="suffix"/>, as one signed string.</summary>
    public static byte[] Around(this ReadOnlySpan<byte> body, string prefix, string suffix = "")
    {
        var head = Encoding.UTF8.GetBytes(prefix);
        var tail = Encoding.UTF8.GetBytes(suffix);
        var content = new byte[head.Length + body.Length + tail.Length];
        head.CopyTo(content, 0);
        body.CopyTo(content.AsSpan(head.Length));
        tail.CopyTo(content, head.Length + body.Length);
        return content;
    }

    /// <summary>Whether <paramref name="candidate"/> is the hex form of <paramref name="expected"/>, compared in constant time.</summary>
    public static bool MatchesHex(this byte[] expected, string? candidate)
    {
        if (string.IsNullOrEmpty(candidate) || candidate.Length != expected.Length * 2)
            return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(candidate));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="candidate"/> is the base64 form of <paramref name="expected"/>, compared in constant time.</summary>
    public static bool MatchesBase64(this byte[] expected, string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
            return false;

        var decoded = new byte[candidate.Length];
        return Convert.TryFromBase64String(candidate, decoded, out var written)
            && CryptographicOperations.FixedTimeEquals(expected, decoded.AsSpan(0, written));
    }

    /// <summary>Parses unix seconds.</summary>
    public static bool TryReadUnixSeconds(this string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds > 253_402_300_799)
            return false;

        timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }

    /// <summary>Whether <paramref name="timestamp"/> lies within <paramref name="tolerance"/> of <paramref name="now"/>; a zero tolerance accepts any time.</summary>
    public static bool IsWithin(this DateTimeOffset timestamp, DateTimeOffset now, TimeSpan tolerance)
        => tolerance <= TimeSpan.Zero || (now - timestamp).Duration() <= tolerance;

    /// <summary>A top-level string or number property of a JSON body, or null when the body is not a JSON object or lacks it.</summary>
    public static string? JsonProperty(this ReadOnlySpan<byte> body, string name)
    {
        var reader = new Utf8JsonReader(body);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return null;

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var matches = reader.ValueTextEquals(name);
                if (!reader.Read())
                    return null;

                if (matches)
                    return reader.TokenType switch
                    {
                        JsonTokenType.String => reader.GetString(),
                        JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
                        _ => null,
                    };

                reader.Skip();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    /// <summary>The first top-level property name other than <paramref name="except"/>, such as a Telegram update's kind.</summary>
    public static string? FirstJsonPropertyExcept(this ReadOnlySpan<byte> body, string except)
    {
        var reader = new Utf8JsonReader(body);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return null;

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString();
                if (name != except)
                    return name;

                reader.Read();
                reader.Skip();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    /// <summary>The values of <paramref name="key"/> in a <c>k=v</c> list, such as <c>t=…,v1=…,v1=…</c>.</summary>
    public static List<string> ValuesOf(this string header, char separator, string key)
    {
        var values = new List<string>();
        foreach (var part in header.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0 && part.AsSpan(0, equals).SequenceEqual(key))
                values.Add(part[(equals + 1)..]);
        }

        return values;
    }
}
