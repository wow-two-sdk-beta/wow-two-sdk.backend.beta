namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>Defines a check a candidate password must pass before the password slice stores it.</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
public interface IUserPasswordValidator<in TUser>
    where TUser : class
{
    /// <summary>Validates <paramref name="password"/> as the next password of <paramref name="user"/>.</summary>
    /// <param name="user">The account the password is for.</param>
    /// <param name="password">The candidate password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The failures; empty when the password passes.</returns>
    Task<IReadOnlyList<IdentityError>> ValidateAsync(TUser user, string password, CancellationToken cancellationToken = default);
}
