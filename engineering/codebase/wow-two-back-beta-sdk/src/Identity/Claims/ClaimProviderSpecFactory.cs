using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Creates built-in <see cref="ClaimProviderSpec"/> values keyed by authentication scheme.</summary>
/// <remarks><see cref="ClaimTypes.Name"/> is a handle for GitHub/GitLab/Twitch/Discord/Twitter/Reddit/Yandex but a display name for Google/Microsoft/Facebook/Spotify/LinkedIn/Amazon, so it feeds a different canonical field per provider.</remarks>
public static class ClaimProviderSpecFactory
{
    // Prefer mapped claim URIs and retain short OIDC names as fallbacks.
    private const string OidcSub = "sub";
    private const string OidcEmail = "email";
    private const string OidcName = "name";
    private const string OidcPreferredUsername = "preferred_username";
    private const string OidcPicture = "picture";

    // Provider URN claim types emitted by the AspNet.Security.OAuth.* libraries.
    private const string GitHubName = "urn:github:name";
    private const string GitLabName = "urn:gitlab:name";
    private const string GitLabAvatar = "urn:gitlab:avatar";
    private const string GooglePicture = "urn:google:picture";
    private const string DiscordAvatarHash = "urn:discord:avatar:hash";
    private const string TwitchDisplayName = "urn:twitch:displayname";
    private const string TwitchProfileImage = "urn:twitch:profileimageurl";
    private const string SpotifyProfilePicture = "urn:spotify:profilepicture";
    private const string MicrosoftObjectId = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string MicrosoftMail = "mail";

    /// <summary>Fresh, mutable scheme → spec map (case-insensitive keys) seeded with every built-in spec.</summary>
    public static Dictionary<string, ClaimProviderSpec> CreateDefault()
    {
        var map = new Dictionary<string, ClaimProviderSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in BuiltIn())
        {
            map[p.Scheme] = p;
        }

        return map;
    }

    /// <summary>Enumerates every built-in provider spec, one per supported scheme.</summary>
    public static IEnumerable<ClaimProviderSpec> BuiltIn()
    {
        // GitHub — ClaimTypes.Name is the login handle; display lives in urn:github:name; avatar is synthesized from the id.
        yield return new ClaimProviderSpec
        {
            Scheme = "GitHub",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [GitHubName],
            UsernameClaims = [ClaimTypes.Name],
            AvatarClaims = [],
            AvatarSynthesizer = static ctx =>
                ctx.UserId is { Length: > 0 } id ? $"https://avatars.githubusercontent.com/u/{id}" : null,
        };

        // Google — OIDC; ClaimTypes.Name is the display name; no username; picture optional.
        yield return new ClaimProviderSpec
        {
            Scheme = "Google",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [GooglePicture, OidcPicture],
        };

        // Microsoft — id prefers the AAD object id (oid) when present; preferred_username doubles as username + email fallback.
        yield return new ClaimProviderSpec
        {
            Scheme = "Microsoft",
            UserIdClaims = [MicrosoftObjectId, ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, MicrosoftMail, OidcEmail, OidcPreferredUsername],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [OidcPreferredUsername],
            AvatarClaims = [],
        };

        // Facebook — ClaimTypes.Name is the display name; no public username/avatar claim.
        yield return new ClaimProviderSpec
        {
            Scheme = "Facebook",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [],
        };

        // Apple — OIDC; name only on first-consent, so display is best-effort; no username/avatar.
        yield return new ClaimProviderSpec
        {
            Scheme = "Apple",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [],
        };

        // Twitter/X — ClaimTypes.Name is the @handle; email rarely present; avatar via provider URL claim if present.
        yield return new ClaimProviderSpec
        {
            Scheme = "Twitter",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = ["urn:twitter:name", OidcName],
            UsernameClaims = [ClaimTypes.Name, "urn:twitter:screenname"],
            AvatarClaims = ["urn:twitter:profileimageurl"],
        };

        // Discord — ClaimTypes.Name is the username handle; avatar synthesized from the avatar-hash claim.
        yield return new ClaimProviderSpec
        {
            Scheme = "Discord",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [],
            UsernameClaims = [ClaimTypes.Name],
            AvatarClaims = [],
            AvatarSynthesizer = static ctx =>
            {
                var hash = ctx.Principal.FindFirst(DiscordAvatarHash)?.Value;
                return ctx.UserId is { Length: > 0 } id && !string.IsNullOrEmpty(hash)
                    ? $"https://cdn.discordapp.com/avatars/{id}/{hash}.png"
                    : null;
            },
        };

        // GitLab — ClaimTypes.Name is the username handle; display in urn:gitlab:name; avatar in urn:gitlab:avatar.
        yield return new ClaimProviderSpec
        {
            Scheme = "GitLab",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [GitLabName],
            UsernameClaims = [ClaimTypes.Name],
            AvatarClaims = [GitLabAvatar],
        };

        // Twitch — ClaimTypes.Name is the login handle; display + avatar in twitch URNs.
        yield return new ClaimProviderSpec
        {
            Scheme = "Twitch",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [TwitchDisplayName],
            UsernameClaims = [ClaimTypes.Name],
            AvatarClaims = [TwitchProfileImage],
        };

        // Spotify — ClaimTypes.Name is the display_name; no username; avatar in urn:spotify:profilepicture.
        yield return new ClaimProviderSpec
        {
            Scheme = "Spotify",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [SpotifyProfilePicture],
        };

        // LinkedIn — OIDC; ClaimTypes.Name is the display name; no username; picture optional.
        yield return new ClaimProviderSpec
        {
            Scheme = "LinkedIn",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [OidcPicture, "picture"],
        };


        // Amazon — ClaimTypes.Name is the display name; no handle.
        yield return new ClaimProviderSpec
        {
            Scheme = "Amazon",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [],
        };

        // Reddit — ClaimTypes.Name is the username handle (login); no email by default; no portable avatar claim.
        yield return new ClaimProviderSpec
        {
            Scheme = "Reddit",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [],
            UsernameClaims = [ClaimTypes.Name],
            AvatarClaims = [],
        };

        // Yandex — ClaimTypes.Name is the login handle; display via urn:yandex:* when present.
        yield return new ClaimProviderSpec
        {
            Scheme = "Yandex",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = ["urn:yandex:display_name", OidcName],
            UsernameClaims = [ClaimTypes.Name],
            AvatarClaims = [],
        };

        // Vkontakte — id + name; email only when scoped; no portable handle.
        yield return new ClaimProviderSpec
        {
            Scheme = "Vkontakte",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = ["urn:vkontakte:photo"],
        };

        // Slack — OIDC-ish; ClaimTypes.Name is the display name; no stable handle.
        yield return new ClaimProviderSpec
        {
            Scheme = "Slack",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [OidcPicture],
        };

        // Notion — bot/user token; id + name + email best-effort; no handle/avatar guarantee.
        yield return new ClaimProviderSpec
        {
            Scheme = "Notion",
            UserIdClaims = [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims = [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims = [ClaimTypes.Name, OidcName],
            UsernameClaims = [],
            AvatarClaims = [OidcPicture, "picture"],
        };

    }
}
