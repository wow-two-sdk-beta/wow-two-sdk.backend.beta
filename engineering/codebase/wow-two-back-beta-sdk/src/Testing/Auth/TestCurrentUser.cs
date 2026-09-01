namespace WoW.Two.Sdk.Backend.Beta.Testing.Auth;

/// <summary>
/// Deterministic current-user stub for handler-level unit tests — resolves to a fixed user id with no HTTP /
/// cookie round-trip. Use when a handler/service under test depends on "who is the current user" but you don't
/// want to stand up a full web host (for that, use <see cref="TestAuthHandler"/>).
/// </summary>
/// <remarks>
///   - <see cref="GetCurrentUserId"/> is name- and signature-compatible with <c>IAuditCurrentUserAccessor.GetCurrentUserId()</c> (<c>Guid? GetCurrentUserId()</c>)
///   - the package references neither that interface nor the core lib, so a test project adapts this instance to it at its own registration
/// </remarks>
public sealed class TestCurrentUser
{
    /// <summary>Creates a guest test user with a fresh random id.</summary>
    public TestCurrentUser() : this(Guid.NewGuid(), TestUserKind.Guest)
    {
    }

    /// <summary>Creates a test user with an explicit id and kind.</summary>
    /// <param name="id">The fixed user id this stub resolves to.</param>
    /// <param name="kind">The identity kind to report.</param>
    public TestCurrentUser(Guid id, TestUserKind kind)
    {
        Id = id;
        Kind = kind;
    }

    /// <summary>The fixed user id every request on this stub resolves to.</summary>
    public Guid Id { get; }

    /// <summary>The identity kind (anonymous / guest / member).</summary>
    public TestUserKind Kind { get; }

    /// <summary>
    /// Returns the current user id, or <c>null</c> when <see cref="Kind"/> is <see cref="TestUserKind.Anonymous"/>.
    /// Signature-compatible with the SDK's <c>IAuditCurrentUserAccessor.GetCurrentUserId()</c>.
    /// </summary>
    public Guid? GetCurrentUserId() => Kind == TestUserKind.Anonymous ? null : Id;
}
