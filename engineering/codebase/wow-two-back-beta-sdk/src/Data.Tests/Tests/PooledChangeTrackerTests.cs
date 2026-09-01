using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>
/// Arch-doc probe PR2, promoted — does a <c>ChangeTracker.Tracked</c> subscription survive a <c>DbContextPool</c>
/// return and re-rent?
/// </summary>
[Collection(DataTestCollection.Name)]
public sealed class PooledChangeTrackerTests(DataTestDb testDb) : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task A_Tracked_subscription_does_not_survive_a_DbContextPool_return_and_re_rent()
    {
        await using var provider = BuildPooledProvider();

        var tracked = 0;
        DataTestDbContext firstRent;

        using (var scope = provider.CreateScope())
        {
            firstRent = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
            firstRent.ChangeTracker.Tracked += (_, _) => Interlocked.Increment(ref tracked);

            firstRent.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "arming" });
            tracked.Should().Be(1); // armed on this rent
        }

        var afterFirstRent = tracked;

        using (var scope = provider.CreateScope())
        {
            var secondRent = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();

            // Without this the case proves nothing: a fresh instance would carry no subscription for trivial reasons.
            secondRent.Should().BeSameAs(firstRent);

            secondRent.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "after-rent" });
        }

        (tracked - afterFirstRent).Should().Be(0); // the pool reset dropped the subscription — a once-per-instance guard is disarmed from rent 2 on
    }

    [Fact]
    public async Task Re_subscribing_on_every_rent_keeps_the_guard_armed()
    {
        await using var provider = BuildPooledProvider();

        var tracked = 0;
        DataTestDbContext? firstRent = null;

        for (var rent = 0; rent < 3; rent++)
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();

            firstRent ??= context;
            context.Should().BeSameAs(firstRent); // the same pooled instance every time, so this is the disarm scenario

            context.ChangeTracker.Tracked += (_, _) => Interlocked.Increment(ref tracked);
            context.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = $"rent-{rent}" });
        }

        tracked.Should().Be(3); // exactly one fire per rent — the mitigation neither drops nor double-counts
    }

    private ServiceProvider BuildPooledProvider()
    {
        var services = new ServiceCollection();

        // AddTestEntityFrameworkCore routes through AddEntityFrameworkCore's default pooled branch (AddDbContextPool).
        services.AddTestEntityFrameworkCore(TestDb);
        return services.BuildServiceProvider();
    }
}
