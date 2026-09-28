namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

/// <summary>Pages lists already in memory, such as a cached catalog, in the same shape database reads use.</summary>
public static class PagingEnumerableExtensions
{
    /// <summary>The offset page of <paramref name="source"/> the request names.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="source">The rows, in the order pages follow.</param>
    /// <param name="request">The page and size asked for.</param>
    /// <param name="maxPageSize">The size cap.</param>
    public static PageDto<T> ToPage<T>(this IEnumerable<T> source, PageApiRequest request, int maxPageSize = PagingConstants.MaxPageSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        var rows = source as IReadOnlyList<T> ?? [.. source];
        var size = PageSizeMapper.Clamp(request.PageSize, maxPageSize);
        var number = Math.Max(request.PageNumber ?? 1, 1);
        return new PageDto<T> { Items = [.. rows.Skip((number - 1) * size).Take(size)], PageNumber = number, PageSize = size, TotalCount = rows.Count };
    }
}
