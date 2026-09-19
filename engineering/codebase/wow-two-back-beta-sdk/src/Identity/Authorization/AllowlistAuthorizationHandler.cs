using Microsoft.AspNetCore.Authorization;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

/// <summary>Handles <see cref="AllowlistRequirement"/> authorization for a principal.</summary>
public sealed class AllowlistAuthorizationHandler : AuthorizationHandler<AllowlistRequirement>
{
    private readonly AllowlistOptions _options;

    /// <summary>Creates the handler from configured options.</summary>
    /// <param name="options">Allowlist options — claim type, allowed values, case sensitivity.</param>
    public AllowlistAuthorizationHandler(AllowlistOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowlistRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        // Empty allowlist ⇒ OPEN: any authenticated principal passes (single-admin default).
        if (_options.Allowed.Count == 0)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var comparer = _options.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        foreach (var claim in context.User.FindAll(_options.ClaimType))
        {
            if (_options.Allowed.Contains(claim.Value, comparer))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}
