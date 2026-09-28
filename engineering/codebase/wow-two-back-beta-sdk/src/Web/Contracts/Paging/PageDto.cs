namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

/// <summary>
/// Represents an offset page of a list read — the UI SDK's <c>Page&lt;T&gt;</c>: the rows, the 1-based page, the size
/// served and the total across pages. Travels as the <c>T</c> of <c>ApiResponse&lt;T&gt;</c>.
/// </summary>
/// <typeparam name="T">The row projection, a <c>*Dto</c>.</typeparam>
public sealed record PageDto<T>
{
    /// <summary>Gets the rows of this page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Gets the 1-based page number served.</summary>
    public required int PageNumber { get; init; }

    /// <summary>Gets the page size served, after clamping.</summary>
    public required int PageSize { get; init; }

    /// <summary>Gets the rows matching across every page.</summary>
    public required long TotalCount { get; init; }
}
