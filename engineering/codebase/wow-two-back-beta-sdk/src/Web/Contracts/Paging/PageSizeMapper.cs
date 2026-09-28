namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

/// <summary>Maps a requested page size to the one served: the default when absent, within 1 and the cap.</summary>
public static class PageSizeMapper
{
    /// <summary>The page size to serve.</summary>
    /// <param name="requested">The requested size.</param>
    /// <param name="maxPageSize">The cap. Default <see cref="PagingConstants.MaxPageSize"/>.</param>
    public static int Clamp(int? requested, int maxPageSize = PagingConstants.MaxPageSize)
    {
        var cap = Math.Max(maxPageSize, 1);
        return Math.Clamp(requested ?? PagingConstants.DefaultPageSize, 1, cap);
    }
}
