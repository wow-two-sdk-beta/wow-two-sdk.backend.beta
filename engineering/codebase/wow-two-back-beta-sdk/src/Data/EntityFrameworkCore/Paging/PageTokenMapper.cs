using System.Buffers.Text;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Paging;

/// <summary>Maps the keys of a page's last row to an opaque token and back: base64url of their JSON array.</summary>
internal static class PageTokenMapper
{
    /// <summary>The token carrying <paramref name="keys"/>.</summary>
    public static string Encode(IReadOnlyList<object?> keys) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(keys));

    /// <summary>The keys a token carries, typed as <paramref name="types"/>; a malformed token answers 400.</summary>
    public static object?[] Decode(string token, IReadOnlyList<Type> types)
    {
        try
        {
            using var document = JsonDocument.Parse(Base64Url.DecodeFromChars(token));
            var elements = document.RootElement.EnumerateArray().ToArray();
            if (elements.Length != types.Count)
                throw new FormatException("The token carries another number of keys.");

            return [.. elements.Select((element, index) => element.Deserialize(types[index]))];
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or NotSupportedException)
        {
            throw AppError.Of(
                AppErrorType.Validation,
                "The page token is not valid; start again from the first page.",
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["messageKey"] = "PageTokenInvalid" }).ToException();
        }
    }
}
