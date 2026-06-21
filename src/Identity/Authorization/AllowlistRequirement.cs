using Microsoft.AspNetCore.Authorization;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

/// <summary>Requirement satisfied when the principal's allowlist claim is in the allowed set — or unconditionally when the set is empty (OPEN); paired with <see cref="AllowlistAuthorizationHandler"/>.</summary>
public sealed class AllowlistRequirement : IAuthorizationRequirement;
