namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>Read-only view of the current request's principal; never writes a cookie, and <see cref="Id"/> is <c>null</c> when anonymous.</summary>
public interface ICurrentUser
{
    /// <summary>The account id (authenticated) or guest id (guest cookie), or <c>null</c> when anonymous or unparseable.</summary>
    Guid? Id { get; }

    /// <summary>How this principal is currently identified.</summary>
    UserKind Kind { get; }
}
