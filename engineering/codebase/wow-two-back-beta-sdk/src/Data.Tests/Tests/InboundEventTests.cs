using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Ordering.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Inbound provider events apply once through the inbox and never regress through the watermark.</summary>
[Collection(DataTestCollection.Name)]
public sealed class InboundEventTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    private static readonly DateTimeOffset Created = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Watermark_AppliesNewerEventsAndIgnoresStaleAndReplayedOnes()
    {
        await using var context = await OpenAsync();
        var id = Guid.NewGuid();
        context.Subscriptions.Add(new SubscriptionRow { Id = id, Status = "incomplete" });
        await context.SaveChangesAsync();

        Assert.Equal(1, await Apply(context, id, Created.AddMinutes(2), "active"));
        Assert.Equal(0, await Apply(context, id, Created.AddMinutes(1), "canceled"));
        Assert.Equal(0, await Apply(context, id, Created.AddMinutes(2), "unpaid"));
        Assert.Equal(1, await Apply(context, id, Created.AddMinutes(3), "past_due"));
        Assert.Equal(0, await Apply(context, Guid.NewGuid(), Created.AddMinutes(4), "active"));

        var row = await context.Subscriptions.AsNoTracking().SingleAsync(subscription => subscription.Id == id);
        Assert.Equal("past_due", row.Status);
        Assert.Equal(Created.AddMinutes(3), row.LastEventAt);
    }

    [Fact]
    public async Task Inbox_AppliesARedeliveredProviderEventOnce()
    {
        await using var created = await OpenAsync();
        var id = Guid.NewGuid();
        created.Subscriptions.Add(new SubscriptionRow { Id = id, Status = "incomplete" });
        await created.SaveChangesAsync();

        var services = new ServiceCollection().AddLogging();
        services.AddScoped(_ => new InboundContext(Options()));
        services.AddEfInbox<InboundContext>();
        await using var provider = services.BuildServiceProvider();

        Assert.True(await DeliverAsync(provider, id));
        Assert.False(await DeliverAsync(provider, id));

        await using var check = new InboundContext(Options());
        Assert.Equal(1, (await check.Subscriptions.SingleAsync(subscription => subscription.Id == id)).Deliveries);
    }

    private static async Task<bool> DeliverAsync(ServiceProvider provider, Guid id)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InboundContext>();
        return await scope.ServiceProvider.GetRequiredService<IInboxProcessor>().ProcessOnceAsync(
            "stripe:evt_1",
            async ct => await context.Subscriptions.Where(subscription => subscription.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(subscription => subscription.Deliveries, subscription => subscription.Deliveries + 1), ct),
            CancellationToken.None);
    }

    private static Task<int> Apply(InboundContext context, Guid id, DateTimeOffset position, string status) =>
        context.Subscriptions
            .Where(subscription => subscription.Id == id)
            .ExecuteUpdateIfNewerAsync(
                subscription => subscription.LastEventAt,
                position,
                setters => setters.SetProperty(subscription => subscription.Status, status));

    private DbContextOptions<InboundContext> Options() =>
        new DbContextOptionsBuilder<InboundContext>().UseNpgsql(TestDb.ConnectionString).UseSnakeCaseNamingConvention().Options;

    private async Task<InboundContext> OpenAsync()
    {
        var context = new InboundContext(Options());
        await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS inbound_subscriptions; DROP TABLE IF EXISTS inbox_messages;");
        await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
        return context;
    }

    private sealed class SubscriptionRow
    {
        public required Guid Id { get; set; }

        public required string Status { get; set; }

        public DateTimeOffset? LastEventAt { get; set; }

        public int Deliveries { get; set; }
    }

    private sealed class InboundContext(DbContextOptions<InboundContext> options) : DbContext(options)
    {
        public DbSet<SubscriptionRow> Subscriptions => Set<SubscriptionRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SubscriptionRow>(entity =>
            {
                entity.ToTable("inbound_subscriptions");
                entity.HasKey(subscription => subscription.Id);
            });
            modelBuilder.ApplyInboxModel();
        }
    }
}
