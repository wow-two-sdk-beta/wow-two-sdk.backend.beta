using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Writes the identity slices' source-generated log events (IDs 4101–4199).</summary>
internal static partial class IdentityLogExtensions
{
    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "The breached-password corpus could not be queried; the check {Outcome}.")]
    internal static partial void BreachCheckUnavailable(this ILogger logger, Exception exception, string outcome);

    [LoggerMessage(EventId = 4111, Level = LogLevel.Information, Message = "A principal for user {UserId} carried a stale security stamp and was rejected.")]
    internal static partial void SecurityStampRejected(this ILogger logger, string userId);

    [LoggerMessage(EventId = 4131, Level = LogLevel.Warning, Message = "A consumed refresh token was presented again; family {FamilyId} was revoked.")]
    internal static partial void RefreshTokenReused(this ILogger logger, Guid familyId);

    [LoggerMessage(EventId = 4141, Level = LogLevel.Debug, Message = "Account email skipped: no email broker, link template or recipient address.")]
    internal static partial void AccountEmailSkipped(this ILogger logger);

    [LoggerMessage(EventId = 4142, Level = LogLevel.Warning, Message = "Account email failed: {Reason}")]
    internal static partial void AccountEmailFailed(this ILogger logger, string? reason);

    [LoggerMessage(EventId = 4121, Level = LogLevel.Warning, Message = "User {UserId} was locked out until {LockoutEnd}.")]
    internal static partial void UserLockedOut(this ILogger logger, string userId, DateTimeOffset lockoutEnd);
}
