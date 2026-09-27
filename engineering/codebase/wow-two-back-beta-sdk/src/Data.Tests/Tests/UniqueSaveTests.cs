using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Uniqueness.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Unique saves report constraint conflicts without breaking the transaction, on SQLite and PostgreSQL.</summary>
[Collection(DataTestCollection.Name)]
public sealed class UniqueSaveTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    private const string KeyConstraint = "ix_keyed_items_key";

    [Fact]
    public async Task SqliteConflict_DetachesTheCandidateAndKeepsTheTransactionUsable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await OpenSqliteAsync(connection);
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Add(Item("taken"));
        Assert.True(await context.TrySaveChangesUniqueAsync());

        var duplicate = Item("taken");
        context.Add(duplicate);
        Assert.False(await context.TrySaveChangesUniqueAsync());
        Assert.Equal(EntityState.Detached, context.Entry(duplicate).State);

        context.Add(Item("free"));
        Assert.True(await context.TrySaveChangesUniqueAsync());
        await transaction.CommitAsync();
        Assert.Equal(["free", "taken"], await context.Items.Select(item => item.Key).OrderBy(key => key).ToListAsync());
    }

    [Fact]
    public async Task GeneratedKeys_RetryPastCollisionsAndStopAtTheBound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await OpenSqliteAsync(connection);
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Add(Item("k1"));
        await context.SaveChangesAsync();

        var candidates = new Queue<string>(["k1", "k1", "k3"]);
        string? saved = await context.SaveWithGeneratedKeyAsync(Item("pending"), (item, key) => item.Key = key, candidates.Dequeue, maxAttempts: 3);
        string? exhausted = await context.SaveWithGeneratedKeyAsync(Item("pending"), (item, key) => item.Key = key, () => "k1", maxAttempts: 2);

        Assert.Equal("k3", saved);
        Assert.Null(exhausted);
        Assert.Equal(2, await context.Items.CountAsync());
    }

    [Fact]
    public async Task UniqueSave_RequiresATransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await OpenSqliteAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.TrySaveChangesUniqueAsync());
    }

    [Fact]
    public async Task PostgresConflict_MatchesOnlyTheNamedConstraint()
    {
        await using var context = await OpenPostgresAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Add(Item("taken"));
        await context.SaveChangesAsync();

        context.Add(Item("taken"));
        Assert.False(await context.TrySaveChangesUniqueAsync(KeyConstraint));

        context.Add(Item("taken"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.TrySaveChangesUniqueAsync("ix_other"));
    }

    private static KeyedItem Item(string key) => new() { Id = Guid.NewGuid(), Key = key };

    private static async Task<KeyedItemContext> OpenSqliteAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();
        var context = new KeyedItemContext(new DbContextOptionsBuilder<KeyedItemContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private async Task<KeyedItemContext> OpenPostgresAsync()
    {
        var context = new KeyedItemContext(new DbContextOptionsBuilder<KeyedItemContext>().UseNpgsql(TestDb.ConnectionString).Options);
        await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS keyed_items");
        await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
        return context;
    }

    private sealed class KeyedItem
    {
        public required Guid Id { get; set; }

        public required string Key { get; set; }
    }

    private sealed class KeyedItemContext(DbContextOptions<KeyedItemContext> options) : DbContext(options)
    {
        public DbSet<KeyedItem> Items => Set<KeyedItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<KeyedItem>(entity =>
            {
                entity.ToTable("keyed_items");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).HasColumnName("id");
                entity.Property(item => item.Key).HasColumnName("key");
                entity.HasIndex(item => item.Key).IsUnique().HasDatabaseName(KeyConstraint);
            });
    }
}
