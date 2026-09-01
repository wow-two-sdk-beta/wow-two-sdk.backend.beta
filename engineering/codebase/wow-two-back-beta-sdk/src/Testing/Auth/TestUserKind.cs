namespace WoW.Two.Sdk.Backend.Beta.Testing.Auth;

/// <summary>The kind of identity a <see cref="TestCurrentUser"/> represents.</summary>
public enum TestUserKind
{
    /// <summary>An anonymous / not-signed-in user.</summary>
    Anonymous,

    /// <summary>A provisioned guest (device-scoped, no account).</summary>
    Guest,

    /// <summary>A fully signed-in member.</summary>
    Member,
}
