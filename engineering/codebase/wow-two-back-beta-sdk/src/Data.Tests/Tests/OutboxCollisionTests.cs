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
/// <remarks>
/// <see cref="PostgresSkipLockedOutboxClaimStrategy"/> opens <b>its own</b> transaction and hand-rolls ~35 lines of
/// commit bridging onto the context's <c>SavedChanges</c> event, settled by an <c>Interlocked</c> flag. EF throws on a
/// second <c>BeginTransaction</c>, so the collision is a standing regression risk, not a one-time question: a
/// dispatcher scope that shares a context with an open unit must fail loudly, and — critically — must not leave its
/// commit bridge attached to someone else's unit.
/// </remarks>
[Collection(DataTestCollection.Name)]
public sealed class OutboxCollisionTests(DataTestDb testDb) : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task Claiming_while_a_unit_is_open_on_the_same_context_throws_instead_of_nesting()
    {
        await using var context = TestDb.NewContext();
        await SeedPendingOutboxRowAsync(context);

        var strategy = new PostgresSkipLockedOutboxClaimStrategy();
        await using var unit = await context.Database.BeginTransactionAsync();

        var claim = async () => await strategy.ClaimPendingAsync(context, 10, CancellationToken.None);

        await claim.Should().ThrowAsync<InvalidOperationException>(); // EF refuses the strategy's own BeginTransaction — the dispatcher must skip units, or the strategy must join one
    }

    [Fact]
    public async Task A_failed_claim_leaves_no_commit_bridge_on_the_callers_unit()
    {
        var id = Guid.NewGuid();
        var strategy = new PostgresSkipLockedOutboxClaimStrategy();

        await using (var context = TestDb.NewContext())
        {
            await SeedPendingOutboxRowAsync(context);

            await using var unit = await context.Database.BeginTransactionAsync();

            context.Widgets.Add(new Widget { Id = id, Name = "uncommitted" });
            await context.SaveChangesAsync();

            var claim = async () => await strategy.ClaimPendingAsync(context, 10, CancellationToken.None);
            await claim.Should().ThrowAsync<InvalidOperationException>();

            // If the strategy had attached its SavedChanges bridge before failing, this save would commit — and
            // dispose — the caller's unit under it.
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
