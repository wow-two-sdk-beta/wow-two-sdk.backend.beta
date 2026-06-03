# WoW.Two.Sdk.Backend.Beta.Data.Dapper

> Dapper conventions for the hot-read path — snake_case mapping, `DateOnly` + `List<T>` type handlers, `IDbConnectionFactory` abstraction.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Data.Dapper
```

## Usage

```csharp
builder.Services.AddDapperConventions();
builder.Services.AddDbConnectionFactory<NpgsqlConnectionFactory>();

public sealed class NpgsqlConnectionFactory(IOptions<DbOptions> opt) : IDbConnectionFactory
{
    public DbConnection Create() => new NpgsqlConnection(opt.Value.ConnectionString);
    public async ValueTask<DbConnection> CreateOpenAsync(CancellationToken ct = default)
    {
        var conn = Create();
        await conn.OpenAsync(ct);
        return conn;
    }
}

// Use it
public sealed class ChannelsQuery(IDbConnectionFactory factory)
{
    public async Task<IReadOnlyList<Channel>> List(CancellationToken ct)
    {
        await using var db = await factory.CreateOpenAsync(ct);
        var rows = await db.QueryAsync<Channel>("SELECT * FROM channels WHERE is_active = TRUE");
        return rows.AsList();
    }
}
```

## What `AddDapperConventions()` does

| Convention | Effect |
|---|---|
| `DefaultTypeMap.MatchNamesWithUnderscores = true` | `image_urls` → `ImageUrls` |
| `DateOnlyTypeHandler` | `DATE` → `DateOnly` |
| `ListTypeHandler<string>` | `TEXT[]` → `List<string>` |

Need more list handlers? Register them yourself:

```csharp
SqlMapper.AddTypeHandler(new ListTypeHandler<int>());
SqlMapper.AddTypeHandler(new ListTypeHandler<Guid>());
```

## See also

- [Dapper](https://github.com/DapperLib/Dapper)
- `…Data.EntityFrameworkCore` — for transactional / write paths
