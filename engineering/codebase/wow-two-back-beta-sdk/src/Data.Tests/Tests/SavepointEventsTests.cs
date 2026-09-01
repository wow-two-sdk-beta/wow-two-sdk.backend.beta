using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>
/// Pins EF's automatic per-<c>SaveChanges</c> savepoint events on Npgsql and the synthetic-frame fallback's depth tags.
/// </summary>
[Collection(DataTestCollection.Name)]
public sealed class SavepointEventsTests(DataTestDb testDb) : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task EF_automatic_per_save_savepoints_raise_the_created_and_rolled_back_events()
    {
        var recorder = new SavepointFrameRecorder();
        await using var context = NewRecordedContext(recorder);

        var id = Guid.NewGuid();
        await using var unit = await context.Database.BeginTransactionAsync();

        context.Widgets.Add(new Widget { Id = id, Name = "first" });
        await context.SaveChangesAsync();

        // Force the save to fail at the provider so EF has to undo the batch: a second row with the same primary key.
        context.ChangeTracker.Clear();
        context.Widgets.Add(new Widget { Id = id, Name = "duplicate" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        await unit.RollbackAsync();

        // Pinned verbatim on EF 10.0.3 / Npgsql 10.0.0 — per save: created → (rolled-back-to, on failure) → released.
        string.Join(" | ", recorder.Events)
            .Should()
            .Be("started | created:auto | released:auto | created:auto | rolledback-to:auto | released:auto | rolledback");

        recorder.AutomaticSavepointsCreated.Should().Be(2);  // one automatic savepoint per SaveChanges inside the caller's unit — the events DO fire
        recorder.AutomaticSavepointRollbacks.Should().Be(1); // the failed batch's rollback to its savepoint is observable
    }

    [Fact]
    public async Task A_caller_created_savepoint_raises_the_created_event()
    {
        var recorder = new SavepointFrameRecorder();
        await using var context = NewRecordedContext(recorder);

        await using var unit = await context.Database.BeginTransactionAsync();
        await unit.CreateSavepointAsync("inner");
        await unit.RollbackToSavepointAsync("inner");
        await unit.RollbackAsync();

        recorder.ExplicitSavepointsCreated.Should().Be(1); // control for the case above — a savepoint the caller names is unambiguously observable
        recorder.Events.Should().Contain("created:explicit").And.Contain("rolledback-to:explicit");
    }

    [Fact]
    public async Task The_synthetic_frame_fallback_tags_depth_from_caller_frames_only()
    {
        var recorder = new SavepointFrameRecorder();
        await using var context = NewRecordedContext(recorder);

        // depth 0 — no unit open
        context.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "outside" });
        await context.SaveChangesAsync();

        await using var unit = await context.Database.BeginTransactionAsync();

        // depth 1 — inside the unit
        context.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "depth-1" });
        await context.SaveChangesAsync();

        await unit.CreateSavepointAsync("inner");

        // depth 2 — inside one caller savepoint
        context.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "depth-2" });
        await context.SaveChangesAsync();

        // rolling back to a savepoint does not release it, so the frame — and the depth — survives
        await unit.RollbackToSavepointAsync("inner");
        context.ChangeTracker.Clear();
        context.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "still-depth-2" });
        await context.SaveChangesAsync();

        await unit.CommitAsync();

        recorder.SyntheticDepthPerSave.Should().Equal(0, 1, 2, 2);   // the fallback's depth tags, derived without any automatic-savepoint event
        recorder.AutomaticSavepointsCreated.Should().BeGreaterThan(0); // EF's own savepoints DID fire here
        recorder.SyntheticDepthPerSave.Should().Equal(0, 1, 2, 2);    // tags unchanged despite EF's savepoints, so the fallback stays correct if they stop firing
    }

    private DataTestDbContext NewRecordedContext(SavepointFrameRecorder recorder)
    {
        var builder = new DbContextOptionsBuilder<DataTestDbContext>();
        TestDb.ApplyProvider(builder); // carries the fixture's conventions, so this context maps the schema EnsureCreated built
        builder.AddInterceptors(recorder);
        return new DataTestDbContext(builder.Options);
    }
}
