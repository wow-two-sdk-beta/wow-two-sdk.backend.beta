namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;

/// <summary>
/// Resolves the current user id for <c>IAuditableBy</c> stamping. Consumers implement this
/// however they like (HTTP context, ambient context, etc.). When no implementation is
/// registered, <c>IAuditableBy</c> fields are left untouched on save.
/// </summary>
public interface IAuditCurrentUserAccessor
{
    /// <summary>Returns the current user id, or <c>null</c> if no user is in scope.</summary>
    Guid? GetCurrentUserId();
}
