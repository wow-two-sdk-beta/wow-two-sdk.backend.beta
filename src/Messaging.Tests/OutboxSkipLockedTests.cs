using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// Verifies <see cref="PostgresSkipLockedOutboxClaimStrategy"/> is multi-instance-safe: two dispatcher/claim loops draining
/// one outbox concurrently claim every pending row exactly once — no double-claim, no lost row — via
/// <c>FOR UPDATE SKIP LOCKED</c> row locks held until each stamp commits.
/// </summary>
public sealed class OutboxSkipLockedTests : IAsyncLifetime
{
    private const int RowCount = 200;
    private const int BatchSize = 7;

    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // The bespoke migrator owns this DDL in production (see Ef.md); create it directly for the test.
        // Run it on a raw command (not ExecuteSqlRaw, which string.Formats the text and would choke on the '{}' default).
        await using var context = NewContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE outbox_messages (
                id               uuid         NOT NULL,
                type             varchar(500) NOT NULL,
                payload          bytea        NOT NULL,
                occurred_on_utc  timestamptz  NOT NULL,
                headers_json     text         NOT NULL DEFAULT '{}',
                processed_on_utc timestamptz  NULL,
                attempts         int          NOT NULL DEFAULT 0,
                error            text         NULL,
                CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
            );
            CREATE INDEX ix_outbox_messages_pending ON outbox_messages (occurred_on_utc) WHERE processed_on_utc IS NULL;
            """;
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Two_concurrent_dispatchers_claim_every_row_exactly_once()
    {
        await SeedPendingRowsAsync(RowCount);

        var strategy = new PostgresSkipLockedOutboxClaimStrategy();
        var claimedByLoop = new ConcurrentDictionary<int, ConcurrentBag<Guid>>
        {
            [1] = [],
            [2] = [],
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var loopA = Task.Run(() => DrainAsync(1, strategy, claimedByLoop[1], cts.Token), cts.Token);
        var loopB = Task.Run(() => DrainAsync(2, strategy, claimedByLoop[2], cts.Token), cts.Token);
        await Task.WhenAll(loopA, loopB);

        // No lost row — every seeded row was stamped processed.
        await using var verify = NewContext();
        var stillPending = await verify.Set<OutboxMessageEntity>().CountAsync(row => row.ProcessedOnUtc == null);
        stillPending.Should().Be(0);

        // No double-claim — the union of both loops' claims is exactly the seeded set, with no duplicates.
        var claimed = claimedByLoop.Values.SelectMany(bag => bag).ToList();
        claimed.Should().HaveCount(RowCount);
        claimed.Distinct().Should().HaveCount(RowCount);

        // The two loops partitioned the work — no id claimed by both.
        claimedByLoop[1].ToHashSet().Overlaps(claimedByLoop[2]).Should().BeFalse();
    }

    // Mirrors the dispatcher's per-pass flow (claim → stamp processed_on_utc → SaveChanges) on its own context/connection,
    // so two loops exercise cross-connection FOR UPDATE SKIP LOCKED exactly like two dispatcher instances would.
    private async Task DrainAsync(int loopId, PostgresSkipLockedOutboxClaimStrategy strategy, ConcurrentBag<Guid> claimed, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var context = NewContext();

            var batch = await strategy.ClaimPendingAsync(context, BatchSize, cancellationToken);
            if (batch.Count == 0)
                return;

            var now = DateTimeOffset.UtcNow;
            foreach (var row in batch)
                row.ProcessedOnUtc = now;

            await context.SaveChangesAsync(cancellationToken); // fires SavedChanges → strategy commits the tx, releases the locks

            foreach (var row in batch)
                claimed.Add(row.Id);
        }
    }

    private async Task SeedPendingRowsAsync(int count)
    {
        await using var context = NewContext();
        var occurredBase = DateTimeOffset.UtcNow.AddMinutes(-count);
        for (var i = 0; i < count; i++)
        {
            context.Set<OutboxMessageEntity>().Add(new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                Type = "test-event",
                Payload = [1, 2, 3],
                OccurredOnUtc = occurredBase.AddMilliseconds(i),
                HeadersJson = "{}",
            });
        }

        await context.SaveChangesAsync();
    }

    private OutboxTestDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new OutboxTestDbContext(options);
    }

    /// <summary>Minimal context mapping the outbox entity with the SDK's snake_case convention (mirrors a consumer's outbox host).</summary>
    private sealed class OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyOutboxModel();
        }
    }
}
