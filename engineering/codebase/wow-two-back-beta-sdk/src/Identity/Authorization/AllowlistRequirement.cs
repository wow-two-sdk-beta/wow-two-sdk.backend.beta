using Microsoft.AspNetCore.Authorization;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

/// <summary>Authorization requirement evaluated by <see cref="AllowlistAuthorizationHandler"/> against the configured allowlist.</summary>
public sealed class AllowlistRequirement : IAuthorizationRequirement;
