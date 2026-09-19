using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest.Serializers;

/// <summary>Serializes guest identity into an authenticated, expiring cookie capability.</summary>
internal sealed class GuestCookieSerializer(IDataProtectionProvider protection, TimeProvider clock)
{
    internal string Serialize(Guid id, string cookieName, TimeSpan lifetime) =>
        Protector(protection, cookieName).Protect(string.Create(CultureInfo.InvariantCulture,
            $"{id:N}|{clock.GetUtcNow().Add(lifetime).UtcTicks}"));

    internal Guid? Deserialize(string? token, string cookieName)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return null;
        try
        {
            var parts = Protector(protection, cookieName).Unprotect(token).Split('|');
            return parts.Length == 2
                && Guid.TryParseExact(parts[0], "N", out var id) && id != Guid.Empty
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expiry)
                && expiry <= DateTimeOffset.MaxValue.UtcTicks && expiry > clock.GetUtcNow().UtcTicks
                    ? id : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static IDataProtector Protector(IDataProtectionProvider provider, string cookieName) =>
        provider.CreateProtector("WoW2.Sdk.Identity.Guest.Cookie.v1", cookieName);
}
