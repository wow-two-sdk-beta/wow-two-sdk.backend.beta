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

/// <summary>
/// Deterministic current-user stub for handler-level unit tests — resolves to a fixed user id with no HTTP /
/// cookie round-trip. Use when a handler/service under test depends on "who is the current user" but you don't
/// want to stand up a full web host (for that, use <see cref="TestAuthHandler"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetCurrentUserId"/> is name- and signature-compatible with the SDK's
/// <c>WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit.IAuditCurrentUserAccessor</c> (<c>Guid? GetCurrentUserId()</c>),
/// so an app's test project — which references both the core lib and this Testing package — can adapt it in one line:
/// </para>
/// <code>
/// // satisfy IAuditCurrentUserAccessor in tests without a real HTTP context
/// var current = new TestCurrentUser();
/// services.AddSingleton&lt;IAuditCurrentUserAccessor&gt;(current); // TestCurrentUser already exposes the matching method
/// </code>
/// <para>
/// This Testing package is intentionally self-contained (no reference to the core lib), so it does not
/// <c>implement</c> that interface directly — apps bind it at the seam. The <see cref="Id"/> / <see cref="Kind"/>
/// shape is ported from the smart-qr <c>ICurrentUser</c> stub for richer handler tests.
/// </para>
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
