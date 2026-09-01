namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>A single failure from an identity operation.</summary>
/// <param name="Code">Stable error code (e.g. <c>DuplicateUserName</c>).</param>
/// <param name="Description">Human-readable description.</param>
public readonly record struct IdentityError(string Code, string Description);
