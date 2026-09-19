using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Identity.Guest.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest;

internal static class GuestRequestExtensions
{
    internal static Guid? ReadGuest(this HttpContext http, string cookieName,
        GuestCookieSerializer serializer)
    {
        // A present null masks the inbound cookie after Clear, without leaking across requests.
        if (http.Items.TryGetValue((typeof(GuestRequestExtensions), cookieName), out var current))
            return current as Guid?;
        return serializer.Deserialize(http.Request.Cookies[cookieName], cookieName);
    }

    internal static void SetGuest(this HttpContext http, string cookieName, Guid? id) =>
        http.Items[(typeof(GuestRequestExtensions), cookieName)] = id;
}
