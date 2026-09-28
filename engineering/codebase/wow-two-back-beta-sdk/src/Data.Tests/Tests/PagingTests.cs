using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Paging;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.Contracts.Paging;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Offset and keyset paging on SQLite and PostgreSQL: sizes, totals, ties, string and Guid keys, projections, bad tokens.</summary>
public sealed class PagingTests : IClassFixture<PagingTests.PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly PostgresFixture _postgres;

    public PagingTests(PostgresFixture postgres) => _postgres = postgres;

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task OffsetPages_ShouldCountClampAndRunPastTheEnd(string provider)
    {
        await using var scope = await OpenAsync(provider);
        var articles = scope.Context.Articles.OrderBy(article => article.Id);

        var first = await articles.ToPageAsync(new PageApiRequest());
        Assert.Equal((1, 20, 45L), (first.PageNumber, first.PageSize, first.TotalCount));
        Assert.Equal(Enumerable.Range(1, 20), first.Items.Select(article => article.Id));

        var third = await articles.ToPageAsync(new PageApiRequest { PageNumber = 3 });
        Assert.Equal(Enumerable.Range(41, 5), third.Items.Select(article => article.Id));
        Assert.Empty((await articles.ToPageAsync(new PageApiRequest { PageNumber = 4 })).Items);
        Assert.Equal(100, (await articles.ToPageAsync(new PageApiRequest { PageSize = 5000 })).PageSize);
        Assert.Equal(1, (await articles.ToPageAsync(new PageApiRequest { PageNumber = -2 })).PageNumber);
        Assert.Equal(10, (await articles.ToPageAsync(new PageApiRequest { PageSize = 50 }, maxPageSize: 10)).PageSize);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task KeysetPages_ShouldWalkEveryRowOnce_AcrossTies(string provider)
    {
        await using var scope = await OpenAsync(provider);
        var articles = scope.Context.Articles;

        Assert.Equal(Enumerable.Range(1, 45), await WalkAsync(token => articles.ToTokenPageAsync(article => article.Id, new TokenPageApiRequest { PageToken = token }), article => article.Id));

        var newestFirst = await articles.OrderByDescending(article => article.PublishedAt).ThenByDescending(article => article.Id).Select(article => article.Id).ToListAsync();
        var walked = await WalkAsync(
            token => articles.ToTokenPageAsync(article => article.PublishedAt, article => article.Id, new TokenPageApiRequest { PageToken = token, PageSize = 7 }, descending: true),
            article => article.Id);
        Assert.Equal(newestFirst, walked);

        var byTitle = await articles.OrderBy(article => article.Title).ThenBy(article => article.Id).Select(article => article.Id).ToListAsync();
        Assert.Equal(byTitle, await WalkAsync(token => articles.ToTokenPageAsync(article => article.Title, article => article.Id, new TokenPageApiRequest { PageToken = token, PageSize = 6 }), article => article.Id));

        var bySlug = await articles.OrderBy(article => article.Slug).Select(article => article.Id).ToListAsync();
        Assert.Equal(bySlug, await WalkAsync(token => articles.ToTokenPageAsync(article => article.Slug, new TokenPageApiRequest { PageToken = token, PageSize = 8 }), article => article.Id));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task KeysetPages_ShouldPageProjections_AndRefuseAlteredTokens(string provider)
    {
        await using var scope = await OpenAsync(provider);
        var rows = scope.Context.Articles.Select(article => new ArticleRowDto { Id = article.Id, Title = article.Title, PublishedAt = article.PublishedAt });

        var first = await rows.ToTokenPageAsync(row => row.PublishedAt, row => row.Id, new TokenPageApiRequest { PageSize = 10 }, descending: true);
        Assert.Equal(10, first.Items.Count);
        Assert.NotNull(first.NextPageToken);
        var second = await rows.ToTokenPageAsync(row => row.PublishedAt, row => row.Id, new TokenPageApiRequest { PageSize = 10, PageToken = first.NextPageToken }, descending: true);
        Assert.Empty(first.Items.Select(row => row.Id).Intersect(second.Items.Select(row => row.Id)));

        var altered = await Assert.ThrowsAsync<AppException>(() => rows.ToTokenPageAsync(row => row.Id, new TokenPageApiRequest { PageToken = "not-a-token" }));
        Assert.Equal(AppErrorType.Validation, altered.Error.Type);
        await Assert.ThrowsAsync<AppException>(() => rows.ToTokenPageAsync(row => row.PublishedAt, row => row.Id, new TokenPageApiRequest { PageToken = PageTokenFor(["x"]) }));
    }

    [Fact]
    public void InMemoryLists_ShouldPageInTheSameShape()
    {
        var page = Enumerable.Range(1, 45).ToPage(new PageApiRequest { PageNumber = 5, PageSize = 10 });

        Assert.Equal([41, 42, 43, 44, 45], page.Items);
        Assert.Equal(45, page.TotalCount);
    }

    private static string PageTokenFor(object?[] keys) => System.Buffers.Text.Base64Url.EncodeToString(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(keys));

    private static async Task<List<int>> WalkAsync<T>(Func<string?, Task<TokenPageDto<T>>> read, Func<T, int> id)
    {
        var ids = new List<int>();
        string? token = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var page = await read(token);
            ids.AddRange(page.Items.Select(id));
            token = page.NextPageToken;
            if (token is null)
                return ids;
        }

        throw new InvalidOperationException("Paging did not end.");
    }

    private async Task<Scope> OpenAsync(string provider)
    {
        DbContextOptions<PagingDbContext> options;
        SqliteConnection? connection = null;
        if (provider == "sqlite")
        {
            connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            options = new DbContextOptionsBuilder<PagingDbContext>().UseSqlite(connection).Options;
        }
        else
        {
            options = new DbContextOptionsBuilder<PagingDbContext>().UseNpgsql(await _postgres.FreshDatabaseAsync()).Options;
        }

        var context = new PagingDbContext(options, sqlite: connection is not null);
        await context.Database.EnsureCreatedAsync();
        context.Articles.AddRange(Enumerable.Range(1, 45).Select(index => new Article
        {
            Id = index,
            Title = $"Title {(char)('A' + (index * 7 % 5))}",
            PublishedAt = Start.AddHours(index / 4),
            Slug = new Guid(index * 7919, (short)index, (short)(index * 3), [(byte)index, 1, 2, 3, 4, 5, 6, 7]),
        }));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new Scope(context, connection);
    }

    public sealed class PostgresFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        private int _databases;

        public async Task<string> FreshDatabaseAsync()
        {
            var name = $"paging_{Interlocked.Increment(ref _databases)}";
            await using (var admin = new Npgsql.NpgsqlConnection(_container.GetConnectionString()))
            {
                await admin.OpenAsync();
                await using var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE {name}", admin);
                await create.ExecuteNonQueryAsync();
            }

            return new Npgsql.NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
        }

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();
    }

    private sealed class Scope(PagingDbContext context, SqliteConnection? connection) : IAsyncDisposable
    {
        public PagingDbContext Context { get; } = context;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            if (connection is not null)
                await connection.DisposeAsync();
        }
    }

    private sealed class PagingDbContext(DbContextOptions<PagingDbContext> options, bool sqlite) : DbContext(options)
    {
        public DbSet<Article> Articles => Set<Article>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Article>().Property(article => article.Id).ValueGeneratedNever();
            if (sqlite)
                modelBuilder.ApplyDateTimeOffsetToBinaryConversion();
        }
    }

    private sealed class Article
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset PublishedAt { get; set; }

        public Guid Slug { get; set; }
    }

    private sealed record ArticleRowDto
    {
        public required int Id { get; init; }

        public required string Title { get; init; }

        public required DateTimeOffset PublishedAt { get; init; }
    }
}
