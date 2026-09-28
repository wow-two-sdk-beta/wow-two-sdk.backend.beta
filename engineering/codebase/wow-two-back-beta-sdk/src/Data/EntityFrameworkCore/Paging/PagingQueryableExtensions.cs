using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Paging;

/// <summary>
/// Pages EF queries into the SDK's wire shapes: offset pages with a total, and keyset pages that continue from an
/// opaque token — steady under inserts, cheap on deep pages, and exact across ties when a unique key comes last.
/// </summary>
public static class PagingQueryableExtensions
{
    /// <summary>
    /// The offset page the request names, with the total across pages. Order <paramref name="query"/> first; an
    /// unordered query pages unpredictably.
    /// </summary>
    /// <typeparam name="T">The row type, often a projected <c>*Dto</c>.</typeparam>
    /// <param name="query">The ordered query.</param>
    /// <param name="request">The page and size asked for.</param>
    /// <param name="maxPageSize">The size cap. Default 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<PageDto<T>> ToPageAsync<T>(this IQueryable<T> query, PageApiRequest request, int maxPageSize = PagingConstants.MaxPageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);
        var size = PageSizeMapper.Clamp(request.PageSize, maxPageSize);
        var number = Math.Max(request.PageNumber ?? 1, 1);
        var total = await query.LongCountAsync(cancellationToken);
        var skip = (long)(number - 1) * size;
        List<T> items = skip >= total ? [] : await query.Skip((int)skip).Take(size).ToListAsync(cancellationToken);
        return new PageDto<T> { Items = items, PageNumber = number, PageSize = size, TotalCount = total };
    }

    /// <summary>The keyset page after the request's token, ordered by one key; the key should be unique, such as the id.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="query">The query, not yet ordered.</param>
    /// <param name="key">The key pages follow.</param>
    /// <param name="request">The token and size.</param>
    /// <param name="descending">Whether pages run from the highest key down.</param>
    /// <param name="maxPageSize">The size cap. Default 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<TokenPageDto<T>> ToTokenPageAsync<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> key, TokenPageApiRequest request, bool descending = false, int maxPageSize = PagingConstants.MaxPageSize, CancellationToken cancellationToken = default)
        => KeysetAsync(query, [key], request, descending, maxPageSize, cancellationToken);

    /// <summary>
    /// The keyset page after the request's token, ordered by <paramref name="first"/> then <paramref name="then"/> —
    /// such as creation time then id, so rows sharing a time are neither skipped nor repeated.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <typeparam name="TFirst">The first key type.</typeparam>
    /// <typeparam name="TThen">The tie-breaking key type; make it unique.</typeparam>
    /// <param name="query">The query, not yet ordered.</param>
    /// <param name="first">The first key.</param>
    /// <param name="then">The tie-breaking key.</param>
    /// <param name="request">The token and size.</param>
    /// <param name="descending">Whether pages run from the highest keys down, as a newest-first feed does.</param>
    /// <param name="maxPageSize">The size cap. Default 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<TokenPageDto<T>> ToTokenPageAsync<T, TFirst, TThen>(this IQueryable<T> query, Expression<Func<T, TFirst>> first, Expression<Func<T, TThen>> then, TokenPageApiRequest request, bool descending = false, int maxPageSize = PagingConstants.MaxPageSize, CancellationToken cancellationToken = default)
        => KeysetAsync(query, [first, then], request, descending, maxPageSize, cancellationToken);

    private static async Task<TokenPageDto<T>> KeysetAsync<T>(IQueryable<T> query, IReadOnlyList<LambdaExpression> keys, TokenPageApiRequest request, bool descending, int maxPageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);
        var size = PageSizeMapper.Clamp(request.PageSize, maxPageSize);
        var row = Expression.Parameter(typeof(T), "row");
        var bodies = keys.Select(key => new ParameterSwap(key.Parameters[0], row).Visit(key.Body)).ToList();

        if (!string.IsNullOrWhiteSpace(request.PageToken))
        {
            var values = PageTokenMapper.Decode(request.PageToken, [.. bodies.Select(body => body.Type)]);
            query = query.Where(Expression.Lambda<Func<T, bool>>(After(bodies, values, descending), row));
        }

        for (var index = 0; index < bodies.Count; index++)
        {
            var method = (index == 0, descending) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending),
            };
            query = (IQueryable<T>)query.Provider.CreateQuery(Expression.Call(
                typeof(Queryable), method, [typeof(T), bodies[index].Type], query.Expression, Expression.Quote(Expression.Lambda(bodies[index], row))));
        }

        var rows = await query.Take(size + 1).ToListAsync(cancellationToken);
        if (rows.Count <= size)
            return new TokenPageDto<T> { Items = rows, PageSize = size };

        rows.RemoveAt(size);
        var last = rows[^1];
        var lastKeys = keys.Select(key => key.Compile().DynamicInvoke(last)).ToList();
        return new TokenPageDto<T> { Items = rows, PageSize = size, NextPageToken = PageTokenMapper.Encode(lastKeys) };
    }

    /// <summary><c>k1 &gt; v1 || (k1 == v1 &amp;&amp; k2 &gt; v2)</c>, or with <c>&lt;</c> when descending, the values parameterized.</summary>
    private static Expression After(List<Expression> keys, object?[] values, bool descending)
    {
        Expression? any = null;
        for (var index = 0; index < keys.Count; index++)
        {
            Expression clause = Compare(keys[index], Parameter(values[index], keys[index].Type), descending);
            for (var previous = index - 1; previous >= 0; previous--)
                clause = Expression.AndAlso(Expression.Equal(keys[previous], Parameter(values[previous], keys[previous].Type)), clause);

            any = any is null ? clause : Expression.OrElse(any, clause);
        }

        return any!;
    }

    /// <summary><paramref name="key"/> beyond <paramref name="value"/>: the operator when the type has one, else <c>CompareTo</c>.</summary>
    private static BinaryExpression Compare(Expression key, Expression value, bool descending)
    {
        try
        {
            return descending ? Expression.LessThan(key, value) : Expression.GreaterThan(key, value);
        }
        catch (InvalidOperationException)
        {
            var compareTo = key.Type.GetMethod(nameof(IComparable.CompareTo), BindingFlags.Public | BindingFlags.Instance, [key.Type])
                ?? throw new NotSupportedException($"Keyset paging cannot compare {key.Type.Name}.");
            var comparison = Expression.Call(key, compareTo, value);
            var zero = Expression.Constant(0);
            return descending ? Expression.LessThan(comparison, zero) : Expression.GreaterThan(comparison, zero);
        }
    }

    /// <summary>The value as a captured field, so EF sends a parameter instead of inlining a literal.</summary>
    private static MemberExpression Parameter(object? value, Type type)
    {
        var box = Activator.CreateInstance(typeof(Box<>).MakeGenericType(type), value)!;
        return Expression.Field(Expression.Constant(box), nameof(Box<object>.Value));
    }

    /// <summary>Holds one key value for a query parameter.</summary>
    /// <typeparam name="TValue">The key type.</typeparam>
    /// <param name="value">The value.</param>
    private sealed class Box<TValue>(TValue value)
    {
        public readonly TValue Value = value;
    }

    /// <summary>Swaps a key lambda's parameter for the shared row parameter.</summary>
    private sealed class ParameterSwap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
