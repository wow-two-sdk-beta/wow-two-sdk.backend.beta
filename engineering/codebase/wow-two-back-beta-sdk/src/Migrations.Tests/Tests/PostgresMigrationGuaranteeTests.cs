using AwesomeAssertions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>States used to verify native PostgreSQL enum registration.</summary>
public enum MigrationDeliveryState
{
    /// <summary>The delivery awaits review.</summary>
    WaitingForReview,
}

/// <summary>Verifies PostgreSQL-owned migration guarantees and documented engine limits.</summary>
[Collection(PostgresMigrationCollection.Name)]
public sealed class PostgresMigrationGuaranteeTests(MigratorPostgresFixture fixture) : MigratorTestBase(fixture)
{
    [Fact]
    public async Task ApplyPending_ShouldSerializeTwoApplicantsAcrossTheCompleteLoop()
    {
        Workspace.Write("001-serialized",
            "select pg_sleep(0.25); create table serialized_item(id integer primary key);",
            "drop table serialized_item;");

        await using var first = CreateMigrator();
        await using var second = CreateMigrator();

        first.CoordinationMode.Should().Be(MigrationCoordinationMode.DatabaseLock);
        second.CoordinationMode.Should().Be(MigrationCoordinationMode.DatabaseLock);

        var results = await Task.WhenAll(
            first.Runner.ApplyPendingAsync("first", CancellationToken.None),
            second.Runner.ApplyPendingAsync("second", CancellationToken.None));

        var applied = results.SelectMany(result => result.ValueOrThrow()).ToList();
        applied.Should().ContainSingle().Which.Should().Be("001-serialized");
        (await first.ReadHistoryAsync()).Should().ContainSingle(row => row.Ordinal == 1);
    }

    [Fact]
    public async Task ApplyPending_ShouldRecoverAnInterruptedConcurrentIndex()
    {
        Workspace.Write("001-baseline", """
            create table indexed_item(id integer primary key, name text not null);
            insert into indexed_item(id, name) values (1, 'duplicate'), (2, 'duplicate');
            """, "drop table indexed_item;");
        Workspace.Write("002-unique-index", """
            -- @no-transaction
            drop index concurrently if exists ux_indexed_item_name;
            create unique index concurrently ux_indexed_item_name on indexed_item(name);
            """, "drop index concurrently if exists ux_indexed_item_name;");

        await using var migrator = CreateMigrator();
        await Assert.ThrowsAsync<PostgresException>(
            () => migrator.Runner.ApplyPendingAsync("first", CancellationToken.None));

        await using (var connection = await migrator.OpenConnectionAsync())
        {
            var valid = await connection.ExecuteScalarAsync<bool>("""
                select i.indisvalid
                from pg_index i
                join pg_class c on c.oid = i.indexrelid
                where c.relname = 'ux_indexed_item_name'
                """);
            valid.Should().BeFalse();
            await connection.ExecuteAsync("delete from indexed_item where id = 2");
        }

        (await migrator.Runner.ApplyPendingAsync("recovery", CancellationToken.None))
            .ValueOrThrow().Should().Equal("002-unique-index");

        await using var verified = await migrator.OpenConnectionAsync();
        (await verified.ExecuteScalarAsync<bool>("""
            select i.indisvalid
            from pg_index i
            join pg_class c on c.oid = i.indexrelid
            where c.relname = 'ux_indexed_item_name'
            """)).Should().BeTrue();
    }

    [Fact]
    public async Task EnumLabel_ShouldCommitBeforeASeparateMigrationUsesIt()
    {
        Workspace.Write("001-enum", """
            create type delivery_state AS enum ('queued');
            create table delivery_item(id integer primary key, state delivery_state not null);
            """, "drop table delivery_item; drop type delivery_state;");
        Workspace.Write("002-label-and-use", """
            alter type delivery_state add value 'waiting_for_review';
            insert into delivery_item(id, state) values (1, 'waiting_for_review');
            """, "-- PostgreSQL enum labels cannot be removed safely.");

        await using var migrator = CreateMigrator();
        await Assert.ThrowsAsync<PostgresException>(
            () => migrator.Runner.ApplyPendingAsync("combined", CancellationToken.None));
        (await migrator.ReadHistoryAsync()).Select(row => row.Ordinal).Should().Equal(1);

        Workspace.OverwriteApply("002-label-and-use",
            "alter type delivery_state add value 'waiting_for_review';");
        Workspace.Write("003-use-label",
            "insert into delivery_item(id, state) values (1, 'waiting_for_review');",
            "delete from delivery_item where id = 1;");

        (await migrator.Runner.ApplyPendingAsync("split", CancellationToken.None))
            .ValueOrThrow().Should().Equal("002-label-and-use", "003-use-label");

        await using var connection = await migrator.OpenConnectionAsync();
        (await connection.ExecuteScalarAsync<string>(
            "select state::text from delivery_item where id = 1")).Should().Be("waiting_for_review");
    }

    [Fact]
    public async Task Rollback_ShouldRemoveHistoryButPreserveIrreversibleEnumLabel()
    {
        Workspace.Write("001-enum", "create type delivery_state AS enum ('queued');", "drop type delivery_state;");
        Workspace.Write("002-label", "alter type delivery_state add value 'waiting_for_review';",
            "-- PostgreSQL enum labels cannot be removed safely.");

        await using var migrator = CreateMigrator(options => options.AllowRollback = true);
        (await migrator.Runner.ApplyPendingAsync("apply", CancellationToken.None)).ValueOrThrow();
        (await migrator.Runner.RollbackAsync(null, CancellationToken.None))
            .ValueOrThrow().Should().Equal("002-label");

        (await migrator.ReadHistoryAsync()).Select(row => row.Ordinal).Should().Equal(1);
        await using var connection = await migrator.OpenConnectionAsync();
        (await connection.ExecuteScalarAsync<int>("""
            select count(*)
            from pg_enum e
            join pg_type t on t.oid = e.enumtypid
            where t.typname = 'delivery_state' and e.enumlabel = 'waiting_for_review'
            """)).Should().Be(1);
    }

    [Fact]
    public async Task NativeEnum_ShouldRoundTripThroughDriverAndEfRegistrations()
    {
        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync("""
                create type delivery_state AS enum ('waiting_for_review');
                create table delivery_record(id integer primary key, state delivery_state not null);
                """);
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(Fixture.ConnectionString);
        dataSourceBuilder.MapEnums(
            CaseStyle.Snake,
            ns => ns == typeof(MigrationDeliveryState).Namespace,
            _ => "delivery_state",
            typeof(MigrationDeliveryState).Assembly);
        await using var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<DeliveryDbContext>();
        optionsBuilder.UseNpgsqlConventional(dataSource, npgsql => npgsql.MapEnum<MigrationDeliveryState>(
            "delivery_state", nameTranslator: new CaseStyleNameTranslator(CaseStyle.Snake)));
        var options = optionsBuilder.Options;

        await using (var context = new DeliveryDbContext(options))
        {
            context.Records.Add(new DeliveryRecord { Id = 1, State = MigrationDeliveryState.WaitingForReview });
            await context.SaveChangesAsync();
        }

        await using var readContext = new DeliveryDbContext(options);
        (await readContext.Records.SingleAsync()).State.Should().Be(MigrationDeliveryState.WaitingForReview);
    }

    private sealed class DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : DbContext(options)
    {
        public DbSet<DeliveryRecord> Records => Set<DeliveryRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeliveryRecord>(entity =>
            {
                entity.ToTable("delivery_record");
                entity.HasKey(record => record.Id);
                entity.Property(record => record.Id).HasColumnName("id");
                entity.Property(record => record.State).HasColumnName("state").HasColumnType("delivery_state");
            });
        }
    }

    private sealed class DeliveryRecord
    {
        public int Id { get; init; }

        public MigrationDeliveryState State { get; init; }
    }
}
