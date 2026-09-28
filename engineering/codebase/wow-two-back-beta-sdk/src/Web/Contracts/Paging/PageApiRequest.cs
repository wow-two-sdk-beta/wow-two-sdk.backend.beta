namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

/// <summary>Binds <c>?pageNumber=&amp;pageSize=</c> for an offset list read; take it with <c>[AsParameters]</c>.</summary>
public sealed record PageApiRequest
{
    /// <summary>Gets the 1-based page; absent or below 1 reads the first.</summary>
    public int? PageNumber { get; init; }

    /// <summary>Gets the rows per page; absent takes the default, and it is clamped to the maximum.</summary>
    public int? PageSize { get; init; }
}
