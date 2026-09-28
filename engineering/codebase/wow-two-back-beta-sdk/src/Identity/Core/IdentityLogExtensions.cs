using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Writes the identity slices' source-generated log events (IDs 4101–4199).</summary>
internal static partial class IdentityLogExtensions
{
    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "The breached-password corpus could not be queried; the check {Outcome}.")]
    internal static partial void BreachCheckUnavailable(this ILogger logger, Exception exception, string outcome);

    [LoggerMessage(EventId = 4111, Level = LogLevel.Information, Message = "A principal for user {UserId} carried a stale security stamp and was rejected.")]
    internal static partial void SecurityStampRejected(this ILogger logger, string userId);

    [LoggerMessage(EventId = 4121, Level = LogLevel.Warning, Message = "User {UserId} was locked out until {LockoutEnd}.")]
    internal static partial void UserLockedOut(this ILogger logger, string userId, DateTimeOffset lockoutEnd);
}
