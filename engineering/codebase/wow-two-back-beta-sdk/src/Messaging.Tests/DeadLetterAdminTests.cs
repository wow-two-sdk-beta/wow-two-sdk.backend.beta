using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>An event whose handler fails until an operator flips <see cref="FlakyToggle"/> — the fix-then-redrive story.</summary>
public sealed record FlakyEvent(string Value) : IEvent;

/// <summary>An event whose handler never recovers — the infinite-redrive guard's subject.</summary>
public sealed record RedrivePoison(string Value) : IEvent;

/// <summary>The "deploy the fix" switch a redrive test flips between laps.</summary>
public sealed class FlakyToggle
{
    private volatile bool _fail = true;

    /// <summary>Whether <see cref="FlakyHandler"/> throws. Written from the test thread, read on a consume worker.</summary>
    public bool Fail
    {
        get => _fail;
        set => _fail = value;
    }
}

/// <summary>
/// Throws while the toggle says to. The toggle is pulled from the provider rather than injected, because this handler is
/// scanned into every host in the assembly and most of them never register one.
/// </summary>
public sealed class FlakyHandler(IServiceProvider services) : IEventHandler<FlakyEvent>
{
    public ValueTask HandleAsync(EventContext<FlakyEvent> context, CancellationToken cancellationToken)
    {
        if (services.GetService<FlakyToggle>() is { Fail: true })
            throw new TimeoutException("flaky downstream");

        return ValueTask.CompletedTask;
    }
}

/// <summary>Always throws — every redrive lap ends back in the dead-letter store.</summary>
public sealed class RedrivePoisonHandler : IEventHandler<RedrivePoison>
{
    public ValueTask HandleAsync(EventContext<RedrivePoison> context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("poison");
}

/// <summary>The operator surface over the dead-letter store: browse, peek, redrive, the redrive cap, purge.</summary>
public sealed class DeadLetterAdminTests
{
    [Fact]
    public async Task Browses_and_filters_the_store()
    {
        await using var harness = await StartAsync(services => services.AddSingleton(new FlakyToggle()));

        await harness.Bus.PublishAsync(new FlakyEvent("triage"), new PublishOptions { MessageId = "dl-browse-1" });
        await harness.DeadLettered.WaitForAsync<FlakyEvent>();

        var admin = Admin(harness);

        // Naming no source is the cross-source browse, which only an IDeadLetterQueryStore can service.
        var all = await BrowseAsync(admin, new DeadLetterQuery());
        all.Should().ContainSingle().Which.MessageId.Should().Be("dl-browse-1");

        (await BrowseAsync(admin, DeadLetterQuery.ForSource(nameof(FlakyEvent)))).Should().ContainSingle();
        (await BrowseAsync(admin, DeadLetterQuery.ForSource("SomeOtherQueue"))).Should().BeEmpty();

        // Simple name matches the recorded full name — an operator triages by failure kind without the namespace.
        (await BrowseAsync(admin, new DeadLetterQuery { ExceptionType = "TimeoutException" })).Should().ContainSingle();
        (await BrowseAsync(admin, new DeadLetterQuery { ExceptionType = "InvalidOperationException" })).Should().BeEmpty();
        (await BrowseAsync(admin, new DeadLetterQuery { ReasonContains = "FLAKY" })).Should().ContainSingle();
        (await BrowseAsync(admin, new DeadLetterQuery { ReasonContains = "not in the reason" })).Should().BeEmpty();
        (await BrowseAsync(admin, new DeadLetterQuery { MinRedriveCount = 1 })).Should().BeEmpty(); // never redriven

        var peeked = await admin.PeekAsync("dl-browse-1", CancellationToken.None);
        peeked.Should().NotBeNull();
        peeked!.Destination.Should().Be(nameof(FlakyEvent));
        peeked.ExceptionType.Should().Be(typeof(TimeoutException).FullName);
        peeked.State.Should().Be(DeadLetterState.DeadLettered);
        (await admin.PeekAsync("no-such-message", CancellationToken.None)).Should().BeNull();

        // Purge is the other query-store-only operation — it deletes without replaying.
        (await admin.PurgeAsync(new DeadLetterQuery(), CancellationToken.None)).Should().Be(1);
        (await BrowseAsync(admin, new DeadLetterQuery())).Should().BeEmpty();
    }

    [Fact]
    public async Task Redrive_re_delivers_the_message()
    {
        var toggle = new FlakyToggle();
        await using var harness = await StartAsync(services => services.AddSingleton(toggle));

        await harness.Bus.PublishAsync(new FlakyEvent("redrive-me"), new PublishOptions { MessageId = "dl-redrive-1" });
        await harness.DeadLettered.WaitForAsync<FlakyEvent>();
        harness.Consumed.Any(m => m.Is<FlakyEvent>() && m.Outcome == ConsumeOutcome.Success).Should().BeFalse();

        toggle.Fail = false; // the fix an operator ships before putting the message back
        (await Admin(harness).RedriveAsync("dl-redrive-1", CancellationToken.None)).Should().Be(RedriveOutcome.Redriven);

        var handled = await harness.Consumed.WaitForAsync(m => m.Is<FlakyEvent>() && m.Outcome == ConsumeOutcome.Success);
        handled[0].MessageId.Should().Be("dl-redrive-1"); // same message, not a copy
        handled[0].Envelope.DeliveryCount.Should().Be(0); // fresh retry budget
        DeadLetterHeaders.ReadRedriveCount(handled[0].Envelope).Should().Be(1); // the marker rode the wire

        (await Admin(harness).PeekAsync("dl-redrive-1", CancellationToken.None)).Should().BeNull(); // replayed out of the store
    }

    [Fact]
    public async Task Redrive_cap_holds_across_laps_via_the_wire_header()
    {
        const int maxRedrives = 2;
        await using var harness = await StartAsync(configureAdmin: o => o.MaxRedrives = maxRedrives);
        var admin = Admin(harness);

        await harness.Bus.PublishAsync(new RedrivePoison("stuck"), new PublishOptions { MessageId = "dl-cap-1" });
        await harness.DeadLettered.WaitForAsync<RedrivePoison>();

        for (var lap = 1; lap <= maxRedrives; lap++)
        {
            (await admin.RedriveAsync("dl-cap-1", CancellationToken.None)).Should().Be(RedriveOutcome.Redriven);

            // The handler is still broken, so the message comes straight back and the store rebuilds the record.
            await harness.DeadLettered.WaitForAsync<RedrivePoison>(count: lap + 1);

            var record = await admin.PeekAsync("dl-cap-1", CancellationToken.None);
            record.Should().NotBeNull();

            // The whole reason the guard reads a header: the transport constructs a brand-new record on each death, so
            // the stored field is back at 0 and only wt-dl-redrive-count still knows how many laps this message ran.
            record!.RedriveCount.Should().Be(0);
            DeadLetterHeaders.ReadRedriveCount(record.Envelope).Should().Be(lap);
            record.EffectiveRedriveCount.Should().Be(lap);
        }

        (await admin.RedriveAsync("dl-cap-1", CancellationToken.None)).Should().Be(RedriveOutcome.LimitReached);

        // Refused means not replayed: no further delivery, so no further death.
        await harness.WaitForIdleAsync();
        harness.DeadLettered.Count<RedrivePoison>().Should().Be(maxRedrives + 1);

        var held = await admin.PeekAsync("dl-cap-1", CancellationToken.None);
        held!.State.Should().Be(DeadLetterState.Quarantined); // QuarantineAtRedriveLimit, so the next bulk pass skips it
        (await admin.RedriveAsync("dl-cap-1", CancellationToken.None)).Should().Be(RedriveOutcome.Quarantined);

        (await BrowseAsync(admin, new DeadLetterQuery())).Should().BeEmpty(); // hidden from a default browse
        (await BrowseAsync(admin, new DeadLetterQuery { QuarantinedOnly = true })).Should().ContainSingle();

        // Released back into the queue it is visible again — and still refused, because the count lives on the envelope.
        (await admin.ReleaseAsync(new DeadLetterQuery { QuarantinedOnly = true }, CancellationToken.None)).Should().Be(1);
        (await BrowseAsync(admin, new DeadLetterQuery())).Should().ContainSingle();
        (await admin.RedriveAsync("dl-cap-1", CancellationToken.None)).Should().Be(RedriveOutcome.LimitReached);
    }

    [Fact]
    public async Task Bulk_redrive_reports_what_it_could_not_put_back()
    {
        await using var harness = await StartAsync(
            services => services.AddSingleton(new FlakyToggle()),
            configureAdmin: o => o.MaxRedrives = 0); // zero disables redrive — the read-only mode the options describe

        await harness.Bus.PublishAsync(new FlakyEvent("bulk"), new PublishOptions { MessageId = "dl-bulk-1" });
        await harness.DeadLettered.WaitForAsync<FlakyEvent>();

        var result = await Admin(harness).RedriveAsync(DeadLetterQuery.ForSource(nameof(FlakyEvent)), CancellationToken.None);

        result.Matched.Should().Be(1);
        result.Redriven.Should().Be(0);
        result.IsComplete.Should().BeFalse();
        result.Failures.Should().ContainSingle().Which.Outcome.Should().Be(RedriveOutcome.LimitReached);
    }

    private static IDeadLetterAdmin Admin(MessagingTestHarness harness)
        => harness.Services.GetRequiredService<IDeadLetterAdmin>();

    private static async Task<IReadOnlyList<DeadLetterRecord>> BrowseAsync(IDeadLetterAdmin admin, DeadLetterQuery query)
    {
        var records = new List<DeadLetterRecord>();
        await foreach (var record in admin.BrowseAsync(query, CancellationToken.None))
            records.Add(record);

        return records;
    }

    private static Task<MessagingTestHarness> StartAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<DeadLetterAdminOptions>? configureAdmin = null)
        => MessagingTestHarness.StartAsync(
            services =>
            {
                services.AddDeadLetterAdmin(configureAdmin);
                services.AddInMemoryDeadLetterQueryStore(); // cross-source browse, by-id lookup and purge
                configureServices?.Invoke(services);
            },
            // One attempt per delivery keeps a redrive lap to a single fault, so the lap count is unambiguous.
            configureBus: o => o.Retry = new RetryConfig(MaxAttempts: 1, Backoff: BackoffKind.None),
            handlerAssemblies: [typeof(RedrivePoisonHandler).Assembly]);
}
