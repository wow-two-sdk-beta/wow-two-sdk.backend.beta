using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Built-in <see cref="ClaimProviderProfile"/> registry keyed by auth scheme; hosts add or override via <see cref="ClaimNormalizationOptions"/>.</summary>
/// <remarks><see cref="ClaimTypes.Name"/> is a handle for GitHub/GitLab/Twitch/Discord/Twitter/Reddit/Yandex but a display name for Google/Microsoft/Facebook/Spotify/LinkedIn/Amazon, so it feeds a different canonical field per provider.</remarks>
public static class ClaimProviderProfiles
{
    // Short OIDC names that survive when MapInboundClaims = false. Listed after the long ClaimTypes.* URI so an
    // OAuth2 handler's mapped URI wins, and the short name is the OIDC fallback.
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

    /// <summary>Fresh, mutable scheme → profile map (case-insensitive keys) seeded with every built-in profile.</summary>
    public static Dictionary<string, ClaimProviderProfile> CreateDefault()
    {
        var map = new Dictionary<string, ClaimProviderProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in BuiltIn())
        {
            map[p.Scheme] = p;
        }

        return map;
    }

    /// <summary>Enumerates every built-in provider profile, one per supported scheme.</summary>
    public static IEnumerable<ClaimProviderProfile> BuiltIn()
    {
        // GitHub — ClaimTypes.Name is the login handle; display lives in urn:github:name; avatar is synthesized from the id.
        yield return new ClaimProviderProfile(
            "GitHub",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [GitHubName],
            UsernameClaims: [ClaimTypes.Name],
            AvatarClaims: [],
            AvatarSynthesizer: static ctx =>
                ctx.UserId is { Length: > 0 } id ? $"https://avatars.githubusercontent.com/u/{id}" : null);

        // Google — OIDC; ClaimTypes.Name is the display name; no username; picture optional.
        yield return new ClaimProviderProfile(
            "Google",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: [GooglePicture, OidcPicture]);

        // Microsoft — id prefers the AAD object id (oid) when present; preferred_username doubles as username + email fallback.
        yield return new ClaimProviderProfile(
            "Microsoft",
            UserIdClaims: [MicrosoftObjectId, ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, MicrosoftMail, OidcEmail, OidcPreferredUsername],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [OidcPreferredUsername],
            AvatarClaims: []);

        // Facebook — ClaimTypes.Name is the display name; no public username/avatar claim.
        yield return new ClaimProviderProfile(
            "Facebook",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: []);

        // Apple — OIDC; name only on first-consent, so display is best-effort; no username/avatar.
        yield return new ClaimProviderProfile(
            "Apple",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: []);

        // Twitter/X — ClaimTypes.Name is the @handle; email rarely present; avatar via provider URL claim if present.
        yield return new ClaimProviderProfile(
            "Twitter",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: ["urn:twitter:name", OidcName],
            UsernameClaims: [ClaimTypes.Name, "urn:twitter:screenname"],
            AvatarClaims: ["urn:twitter:profileimageurl"]);

        // Discord — ClaimTypes.Name is the username handle; avatar synthesized from the avatar-hash claim.
        yield return new ClaimProviderProfile(
            "Discord",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [],
            UsernameClaims: [ClaimTypes.Name],
            AvatarClaims: [],
            AvatarSynthesizer: static ctx =>
            {
                var hash = ctx.Principal.FindFirst(DiscordAvatarHash)?.Value;
                return ctx.UserId is { Length: > 0 } id && !string.IsNullOrEmpty(hash)
                    ? $"https://cdn.discordapp.com/avatars/{id}/{hash}.png"
                    : null;
            });

        // GitLab — ClaimTypes.Name is the username handle; display in urn:gitlab:name; avatar in urn:gitlab:avatar.
        yield return new ClaimProviderProfile(
            "GitLab",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [GitLabName],
            UsernameClaims: [ClaimTypes.Name],
            AvatarClaims: [GitLabAvatar]);

        // Twitch — ClaimTypes.Name is the login handle; display + avatar in twitch URNs.
        yield return new ClaimProviderProfile(
            "Twitch",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [TwitchDisplayName],
            UsernameClaims: [ClaimTypes.Name],
            AvatarClaims: [TwitchProfileImage]);

        // Spotify — ClaimTypes.Name is the display_name; no username; avatar in urn:spotify:profilepicture.
        yield return new ClaimProviderProfile(
            "Spotify",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: [SpotifyProfilePicture]);

        // LinkedIn — OIDC; ClaimTypes.Name is the display name; no username; picture optional.
        yield return new ClaimProviderProfile(
            "LinkedIn",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: [OidcPicture, "picture"]);

        // ── Best-effort ("verify" in claims.md): id + email where available; ClaimTypes.Name is a handle only
        //    for Reddit/Yandex, a display name for Amazon, and unknown for Slack/Notion/Vkontakte. ──

        // Amazon — ClaimTypes.Name is the display name; no handle.
        yield return new ClaimProviderProfile(
            "Amazon",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: []);

        // Reddit — ClaimTypes.Name is the username handle (login); no email by default; no portable avatar claim.
        yield return new ClaimProviderProfile(
            "Reddit",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [],
            UsernameClaims: [ClaimTypes.Name],
            AvatarClaims: []);

        // Yandex — ClaimTypes.Name is the login handle; display via urn:yandex:* when present.
        yield return new ClaimProviderProfile(
            "Yandex",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: ["urn:yandex:display_name", OidcName],
            UsernameClaims: [ClaimTypes.Name],
            AvatarClaims: []);

        // Vkontakte — id + name; email only when scoped; no portable handle.
        yield return new ClaimProviderProfile(
            "Vkontakte",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: ["urn:vkontakte:photo"]);

        // Slack — OIDC-ish; ClaimTypes.Name is the display name; no stable handle.
        yield return new ClaimProviderProfile(
            "Slack",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: [OidcPicture]);

        // Notion — bot/user token; id + name + email best-effort; no handle/avatar guarantee.
        yield return new ClaimProviderProfile(
            "Notion",
            UserIdClaims: [ClaimTypes.NameIdentifier, OidcSub],
            EmailClaims: [ClaimTypes.Email, OidcEmail],
            DisplayNameClaims: [ClaimTypes.Name, OidcName],
            UsernameClaims: [],
            AvatarClaims: [OidcPicture, "picture"]);
    }
}
