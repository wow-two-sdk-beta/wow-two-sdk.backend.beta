using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

/// <summary>Accesses stored user tokens through the context hosting the identity schema.</summary>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
public sealed class EfUserTokenRepository<TKey>(DbContext context) : IUserTokenRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private DbSet<IdentityUserToken<TKey>> Tokens => context.Set<IdentityUserToken<TKey>>();

    /// <inheritdoc />
    public async Task<string?> GetAsync(TKey userId, string loginProvider, string name, CancellationToken cancellationToken = default)
        => (await FindAsync(userId, loginProvider, name, cancellationToken))?.Value;

    /// <inheritdoc />
    public async Task SetAsync(TKey userId, string loginProvider, string name, string value, CancellationToken cancellationToken = default)
    {
        var token = await FindAsync(userId, loginProvider, name, cancellationToken);
        if (token is null)
            Tokens.Add(new IdentityUserToken<TKey> { UserId = userId, LoginProvider = loginProvider, Name = name, Value = value });
        else
            token.Value = value;

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(TKey userId, string loginProvider, string name, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(userId, loginProvider, name, cancellationToken) is not { } token)
            return;

        Tokens.Remove(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<IdentityUserToken<TKey>?> FindAsync(TKey userId, string loginProvider, string name, CancellationToken cancellationToken)
        => await Tokens.FindAsync([userId, loginProvider, name], cancellationToken);
}
