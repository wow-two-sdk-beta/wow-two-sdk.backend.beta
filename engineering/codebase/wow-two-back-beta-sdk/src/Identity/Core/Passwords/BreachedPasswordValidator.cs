using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>Validates that a candidate password is absent from the known breach corpus.</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <param name="client">The range API client.</param>
/// <param name="options">Threshold and failure behavior.</param>
/// <param name="logger">Receives the unreachable-corpus warning.</param>
public sealed class BreachedPasswordValidator<TUser>(
    IPwnedPasswordsClient client,
    BreachedPasswordOptions options,
    ILogger<BreachedPasswordValidator<TUser>> logger) : IUserPasswordValidator<TUser>
    where TUser : class
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<IdentityError>> ValidateAsync(TUser user, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);

        long count;
        try
        {
            count = await client.GetBreachCountAsync(password, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.BreachCheckUnavailable(exception, options.FailOpen ? "passed" : "failed");
            return options.FailOpen
                ? []
                : [new IdentityError { Code = IdentityErrorCodeConstants.PasswordBreachCheckUnavailable, Description = "The password could not be checked; try again." }];
        }

        return count >= options.MinimumBreachCount
            ? [new IdentityError { Code = IdentityErrorCodeConstants.PasswordBreached, Description = "This password appears in a known data breach; choose another." }]
            : [];
    }
}
