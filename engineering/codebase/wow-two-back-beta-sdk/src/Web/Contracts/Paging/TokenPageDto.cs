using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

/// <summary>
/// Represents a keyset page of a list read — the UI SDK's <c>TokenPage&lt;T&gt;</c>: the rows, the size served and the
/// opaque token of the next page, absent on the last one. Travels as the <c>T</c> of <c>ApiResponse&lt;T&gt;</c>.
/// </summary>
/// <typeparam name="T">The row projection, a <c>*Dto</c>.</typeparam>
public sealed record TokenPageDto<T>
{
    /// <summary>Gets the rows of this page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Gets the page size served, after clamping.</summary>
    public required int PageSize { get; init; }

    /// <summary>Gets the token that reads the next page; absent when this is the last page.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextPageToken { get; init; }
}
