using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;
using WoW.Two.Sdk.Backend.Beta.Data.Specs;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>One entity spec shapes Dapper's SQL: renamed table and columns, ignored and store-written properties, stamps and soft delete.</summary>
[Collection(DataTestCollection.Name)]
public sealed class DapperSpecTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    private static readonly EntitySpecRegistry Specs = new EntitySpecRegistry().Add<CatalogItem>(builder =>
    {
        builder.ToTable("spec_catalog");
        builder.Property(item => item.Id).HasColumnName("item_id");
        builder.Property(item => item.Title).HasColumnName("headline");
        builder.Property(item => item.PriceMinor).HasColumnName("price_cents");
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("now()");
        builder.Ignore(item => item.Notes);
        builder.HasConcurrencyToken(item => item.Stamp, ConcurrencyTokenKind.Stamp);
        builder.HasSoftDelete(item => item.IsArchived);
    });

    [Fact]
    public async Task RegisteredSpec_ShouldRenameIgnoreAndGuardWrites()
    {
        await using var source = NpgsqlDataSource.Create(TestDb.ConnectionString);
        await PrepareTableAsync(source);
        var repository = new DapperRepository<CatalogItem, Guid>(new DataSourceConnectionFactory(source), new SqlNamingOptions(), specs: Specs);

        var item = new CatalogItem { Id = Guid.NewGuid(), Title = "Lamp", PriceMinor = 1999, Notes = "kept in memory only" };
        await repository.CreateAsync(item);
        item.Stamp.Should().NotBeNullOrEmpty("an insert writes the first stamp");

        var read = (await repository.GetByIdAsync(item.Id))!;
        (read.Title, read.PriceMinor, read.Notes).Should().Be(("Lamp", 1999L, null));
        read.CreatedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5), "the store's default fills the column the insert left out");
        var stale = (await repository.GetByIdAsync(item.Id))!;

        read.Title = "Desk lamp";
        await repository.UpdateAsync(read);
        read.Stamp.Should().NotBe(stale.Stamp);
        stale.Title = "Old title";
        await FluentActions.Awaiting(() => repository.UpdateAsync(stale)).Should().ThrowAsync<ConcurrencyConflictException>();
        await FluentActions.Awaiting(() => repository.DeleteAsync(stale)).Should().ThrowAsync<ConcurrencyConflictException>();

        read.IsArchived = true;
        await repository.UpdateAsync(read);
        (await repository.GetByIdAsync(item.Id)).Should().BeNull("archived rows leave generated reads");
        (await repository.CountAsync()).Should().Be(0);
        (await repository.ExistsAsync(item.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task UnsupportedSpec_ShouldThrow_UnlessTheHostSaysSkip()
    {
        await using var source = NpgsqlDataSource.Create(TestDb.ConnectionString);
        var factory = new DataSourceConnectionFactory(source);
        var composite = new EntitySpecRegistry().Add<CatalogItem>(builder => builder.HasKey(item => item.Id, item => item.Title));

        FluentActions.Invoking(() => new DapperRepository<CatalogItem, Guid>(factory, new SqlNamingOptions(), specs: composite))
            .Should().Throw<UnsupportedSpecException>().Which.Features.Should().ContainSingle(feature => feature.Contains("keys on Id", StringComparison.Ordinal));
        FluentActions.Invoking(() => new DapperRepository<CatalogItem, Guid>(
                factory,
                new SqlNamingOptions(),
                specs: composite,
                specOptions: Options.Create(new EntitySpecOptions { Unsupported = UnsupportedSpecMode.Skip })))
            .Should().NotThrow();
    }

    private static async Task PrepareTableAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("""
            CREATE TABLE IF NOT EXISTS spec_catalog (
                item_id uuid PRIMARY KEY,
                headline text NOT NULL,
                price_cents bigint NOT NULL,
                created_at timestamp NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                stamp text NOT NULL,
                is_archived boolean NOT NULL DEFAULT FALSE);
            DELETE FROM spec_catalog;
            """);
        await command.ExecuteNonQueryAsync();
    }

    public sealed class CatalogItem : IKeyedEntity<Guid>, IHasTableName
    {
        public static string TableName => "catalog_items";

        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public long PriceMinor { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? Notes { get; set; }

        public string Stamp { get; set; } = string.Empty;

        public bool IsArchived { get; set; }
    }
}
