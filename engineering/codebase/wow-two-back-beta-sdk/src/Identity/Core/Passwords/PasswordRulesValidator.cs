namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>Validates a candidate password against the configured <see cref="PasswordOptions"/>.</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="options">Identity options carrying the password rules.</param>
public sealed class PasswordRulesValidator<TUser, TKey>(IdentityCoreOptions options) : IUserPasswordValidator<TUser>
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <inheritdoc />
    public Task<IReadOnlyList<IdentityError>> ValidateAsync(TUser user, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var rules = options.Password;
        var errors = new List<IdentityError>();

        if (password.Length < rules.MinLength)
            Add(errors, IdentityErrorCodeConstants.PasswordTooShort, $"Passwords must be at least {rules.MinLength} characters.");
        if (password.Length > rules.MaxLength)
            Add(errors, IdentityErrorCodeConstants.PasswordTooLong, $"Passwords must be at most {rules.MaxLength} characters.");
        if (rules.RequireDigit && !password.Any(char.IsAsciiDigit))
            Add(errors, IdentityErrorCodeConstants.PasswordRequiresDigit, "Passwords must contain a digit.");
        if (rules.RequireLowercase && !password.Any(char.IsLower))
            Add(errors, IdentityErrorCodeConstants.PasswordRequiresLower, "Passwords must contain a lowercase letter.");
        if (rules.RequireUppercase && !password.Any(char.IsUpper))
            Add(errors, IdentityErrorCodeConstants.PasswordRequiresUpper, "Passwords must contain an uppercase letter.");
        if (rules.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            Add(errors, IdentityErrorCodeConstants.PasswordRequiresNonAlphanumeric, "Passwords must contain a symbol.");
        if (password.Distinct().Count() < rules.RequiredUniqueChars)
            Add(errors, IdentityErrorCodeConstants.PasswordRequiresUniqueChars, $"Passwords must use at least {rules.RequiredUniqueChars} different characters.");
        if (rules.RejectAccountIdentifiers && (MatchesIdentifier(password, user.UserName) || MatchesIdentifier(password, user.Email)))
            Add(errors, IdentityErrorCodeConstants.PasswordMatchesAccount, "Passwords must differ from the user name and email.");

        return Task.FromResult<IReadOnlyList<IdentityError>>(errors);
    }

    private static bool MatchesIdentifier(string password, string? identifier)
        => !string.IsNullOrEmpty(identifier) && string.Equals(password, identifier, StringComparison.OrdinalIgnoreCase);

    private static void Add(List<IdentityError> errors, string code, string description)
        => errors.Add(new IdentityError { Code = code, Description = description });
}
