using AwesomeAssertions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>
/// Arch-doc probe PR4, promoted — the outbox claim strategy versus an open session unit on the same context.
/// </summary>
[Collection(DataTestCollection.Name)]
public sealed class OutboxCollisionTests(DataTestDb testDb) : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task Claiming_while_a_unit_is_open_on_the_same_context_throws_instead_of_nesting()
    {
        await using var context = TestDb.NewContext();
        await SeedPendingOutboxRowAsync(context);

        var strategy = new PostgresSkipLockedOutboxClaimRepository();
        await using var unit = await context.Database.BeginTransactionAsync();

        var claim = async () => await strategy.ClaimPendingAsync(context, 10, CancellationToken.None);

        await claim.Should().ThrowAsync<InvalidOperationException>(); // EF refuses the strategy's own BeginTransaction
    }

    [Fact]
    public async Task A_failed_claim_leaves_no_commit_bridge_on_the_callers_unit()
    {
        var id = Guid.NewGuid();
        var strategy = new PostgresSkipLockedOutboxClaimRepository();

        await using (var context = TestDb.NewContext())
        {
            await SeedPendingOutboxRowAsync(context);

            await using var unit = await context.Database.BeginTransactionAsync();

            context.Widgets.Add(new Widget { Id = id, Name = "uncommitted" });
            await context.SaveChangesAsync();

            var claim = async () => await strategy.ClaimPendingAsync(context, 10, CancellationToken.None);
            await claim.Should().ThrowAsync<InvalidOperationException>();

            // A second save inside the caller's unit — it stays uncommitted, so no bridge took the unit over.
            context.Widgets.Single(widget => widget.Id == id).Name = "still-uncommitted";
            await context.SaveChangesAsync();

            await unit.RollbackAsync();
        }

        await using var connection = await TestDb.OpenConnectionAsync();
        var surviving = await connection.ExecuteScalarAsync<long>(
            "select count(*) from widgets where id = @id",
            new { id });

        surviving.Should().Be(0); // the unit rolled back whole — the strategy never took it over
    }

    private static async Task SeedPendingOutboxRowAsync(DataTestDbContext context)
    {
        context.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = "test-event",
            Payload = [1, 2, 3],
            ContentType = "application/json",
            OccurredOnUtc = DateTimeOffset.UtcNow,
            HeadersJson = "{}",
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
