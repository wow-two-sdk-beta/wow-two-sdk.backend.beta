# Web.Contracts.Paging

List reads in the shape the UI SDK consumes: offset pages (`Page<T>` there, `PageDto<T>` here) and keyset pages
(`TokenPage<T>` / `TokenPageDto<T>`), bound from the query string and read from EF in one call.

## Quick start

```csharp
// offset: ?pageNumber=2&pageSize=20
app.MapGet("/codes", async ([AsParameters] PageApiRequest page, AppDbContext db, CancellationToken ct) =>
    Results.Ok(ApiResponse<PageDto<CodeRowDto>>.Ok(await db.Codes
        .OrderBy(code => code.Id)
        .Select(code => new CodeRowDto { Id = code.Id, Title = code.Title })
        .ToPageAsync(page, cancellationToken: ct))));

// keyset, newest first: ?pageToken=…&pageSize=50
var feed = await db.Events.Select(e => new EventRowDto { Id = e.Id, At = e.At })
    .ToTokenPageAsync(e => e.At, e => e.Id, request, descending: true, cancellationToken: ct);
```

| Shape | Fields | Pair with |
|---|---|---|
| `PageDto<T>` | `items`, `pageNumber`, `pageSize`, `totalCount` | `useAppPaginatedQuery` |
| `TokenPageDto<T>` | `items`, `pageSize`, `nextPageToken` (absent on the last page) | `useAppInfiniteQuery` |

## Notes

- Sizes default to 20 and cap at 100; pass `maxPageSize` to raise the cap for one read.
- Offset pages need an ordered query; a page past the end is empty with the true total.
- Keyset pages order by the keys themselves; end with a unique key (the id) so ties never skip or repeat rows.
- Keys compare with operators, or `CompareTo` for strings and Guids; EF sends key values as parameters.
- A token is base64url JSON of the last row's keys; an altered one answers 400 (`messageKey` `PageTokenInvalid`).
- `IEnumerable<T>.ToPage(request)` pages lists already in memory.
