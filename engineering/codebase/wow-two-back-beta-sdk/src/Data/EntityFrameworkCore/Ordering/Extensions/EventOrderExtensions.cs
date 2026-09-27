using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Ordering.Extensions;

/// <summary>Extends EF Core updates with a watermark that ignores events older than the state already applied.</summary>
public static class EventOrderExtensions
{
    /// <summary>Applies <paramref name="setters"/> and advances the watermark only where it is older than <paramref name="position"/>.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TPosition">The watermark type, such as <see cref="DateTimeOffset"/> or a sequence number.</typeparam>
    /// <param name="rows">The rows the event targets, already filtered to its subject.</param>
    /// <param name="watermark">The column holding the last applied event's position; null means none yet.</param>
    /// <param name="position">The incoming event's position, such as the provider's creation time.</param>
    /// <param name="setters">The event's column updates.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>The rows updated; zero when the event is stale, a replay, or its subject is absent.</returns>
    /// <remarks>
    ///   - one statement compares and writes, so concurrent deliveries cannot interleave between check and update
    ///   - an equal position counts as stale, so a replayed event changes nothing
    /// </remarks>
    public static Task<int> ExecuteUpdateIfNewerAsync<TEntity, TPosition>(
        this IQueryable<TEntity> rows,
        Expression<Func<TEntity, TPosition?>> watermark,
        TPosition position,
        Action<UpdateSettersBuilder<TEntity>> setters,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TPosition : struct
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(watermark);
        ArgumentNullException.ThrowIfNull(setters);

        TPosition? incoming = position;
        Expression<Func<TPosition?>> parameter = () => incoming;
        Expression current = watermark.Body;
        Expression<Func<TEntity, bool>> newer = Expression.Lambda<Func<TEntity, bool>>(
            Expression.OrElse(
                Expression.Equal(current, Expression.Constant(null, typeof(TPosition?))),
                Expression.LessThan(current, parameter.Body)),
            watermark.Parameters);

        return rows.Where(newer).ExecuteUpdateAsync(
            builder =>
            {
                setters(builder);
                builder.SetProperty(watermark, incoming);
            },
            cancellationToken);
    }
}
