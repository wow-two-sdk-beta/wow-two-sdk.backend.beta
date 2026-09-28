using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.PersonalData;

/// <summary>
/// Provides the personal-data rights of an account: export everything the SDK and the product hold for the user, and
/// delete the account — the product's handlers first, then every identity row, in one transaction when none is open.
/// Secrets (password hash, tokens, keys) are never exported.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
/// <param name="exporters">The product's sections, and roles when that slice is registered.</param>
/// <param name="handlers">The product's deletion handlers.</param>
/// <param name="time">Stamps the export.</param>
public sealed class UserPersonalDataService<TUser, TKey>(
    DbContext context,
    IEnumerable<IPersonalDataExporter<TUser>> exporters,
    IEnumerable<IAccountDeletionHandler<TUser>> handlers,
    TimeProvider time)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Exports the user's identity data and every product section.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Two exporters claim one section.</exception>
    public async Task<PersonalDataExportModel> ExportAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var sections = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["account"] = new
            {
                Id = Convert.ToString(user.Id, CultureInfo.InvariantCulture),
                user.UserName,
                user.Email,
                user.EmailConfirmed,
                user.PhoneNumber,
                user.PhoneNumberConfirmed,
                user.TwoFactorEnabled,
                user.LockoutEnd,
            },
        };
        if (Mapped<IdentityUserClaim<TKey>>())
            sections["claims"] = await context.Set<IdentityUserClaim<TKey>>().AsNoTracking().Where(c => c.UserId.Equals(user.Id)).Select(c => new { Type = c.ClaimType, Value = c.ClaimValue }).ToListAsync(cancellationToken);
        if (Mapped<IdentityUserLogin<TKey>>())
            sections["logins"] = await context.Set<IdentityUserLogin<TKey>>().AsNoTracking().Where(l => l.UserId.Equals(user.Id)).Select(l => new { Provider = l.LoginProvider, DisplayName = l.ProviderDisplayName }).ToListAsync(cancellationToken);
        if (Mapped<IdentityPasskey<TKey>>())
            sections["passkeys"] = await context.Set<IdentityPasskey<TKey>>().AsNoTracking().Where(p => p.UserId.Equals(user.Id)).Select(p => new { p.Name, p.CreatedAt, p.LastUsedAt }).ToListAsync(cancellationToken);
        if (Mapped<IdentityRefreshToken<TKey>>())
            sections["sessions"] = await context.Set<IdentityRefreshToken<TKey>>().AsNoTracking().Where(t => t.UserId.Equals(user.Id)).Select(t => new { t.CreatedAt, t.ExpiresAt, t.RevokedAt }).ToListAsync(cancellationToken);

        foreach (var exporter in exporters)
        {
            if (!sections.TryAdd(exporter.Section, await exporter.ExportAsync(user, cancellationToken)))
                throw new InvalidOperationException($"Two personal-data exporters claim the section '{exporter.Section}'.");
        }

        return new PersonalDataExportModel { GeneratedAt = time.GetUtcNow(), Sections = sections };
    }

    /// <summary>
    /// Deletes the account: the product's handlers, then roles, claims, logins, stored tokens, sessions, passkeys and the
    /// user row. Sessions end with the refresh tokens; issued access tokens live until they expire.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        foreach (var handler in handlers)
            await handler.HandleAsync(user, cancellationToken);

        await using var transaction = context.Database.CurrentTransaction is null && context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await DeleteRowsAsync<IdentityUserRole<TKey>>(row => row.UserId.Equals(user.Id), cancellationToken);
        await DeleteRowsAsync<IdentityUserClaim<TKey>>(row => row.UserId.Equals(user.Id), cancellationToken);
        await DeleteRowsAsync<IdentityUserLogin<TKey>>(row => row.UserId.Equals(user.Id), cancellationToken);
        await DeleteRowsAsync<IdentityUserToken<TKey>>(row => row.UserId.Equals(user.Id), cancellationToken);
        await DeleteRowsAsync<IdentityRefreshToken<TKey>>(row => row.UserId.Equals(user.Id), cancellationToken);
        await DeleteRowsAsync<IdentityPasskey<TKey>>(row => row.UserId.Equals(user.Id), cancellationToken);
        await context.Set<TUser>().Where(row => row.Id.Equals(user.Id)).ExecuteDeleteAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> DeleteRowsAsync<TRow>(System.Linq.Expressions.Expression<Func<TRow, bool>> owned, CancellationToken cancellationToken)
        where TRow : class
        => Mapped<TRow>() ? context.Set<TRow>().Where(owned).ExecuteDeleteAsync(cancellationToken) : Task.FromResult(0);

    private bool Mapped<TRow>() => context.Model.FindEntityType(typeof(TRow)) is not null;
}
