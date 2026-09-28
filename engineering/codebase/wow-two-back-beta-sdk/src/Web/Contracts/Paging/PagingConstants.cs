namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

/// <summary>Holds the page sizes a list read falls back to and is capped at.</summary>
public static class PagingConstants
{
    /// <summary>Holds the page size when a request names none.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Holds the largest page size served unless a read raises it.</summary>
    public const int MaxPageSize = 100;
}
