namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

/// <summary>Binds <c>?pageToken=&amp;pageSize=</c> for a keyset list read; take it with <c>[AsParameters]</c>.</summary>
public sealed record TokenPageApiRequest
{
    /// <summary>Gets the <c>nextPageToken</c> of the previous page; absent reads the first page.</summary>
    public string? PageToken { get; init; }

    /// <summary>Gets the rows per page; absent takes the default, and it is clamped to the maximum.</summary>
    public int? PageSize { get; init; }
}
