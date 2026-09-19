using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging.Trackers;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// A running event bus plus the assertions to interrogate it: what was published, consumed, faulted and
/// dead-lettered, and a wait that returns when the bus goes quiet. Turns a messaging test into
/// arrange harness → publish → await an assertion.
/// </summary>
/// <remarks>
///   - watches the real pipeline through the observer seam, never altering routing, settlement, retries or dead-lettering
///   - works against the in-memory transport and a broker-backed host alike — see <see cref="Attach"/>
///   - to assert something did not happen, await <see cref="WaitForIdleAsync"/> rather than a <c>Task.Delay</c>
/// </remarks>
public sealed class MessagingTestHarness : IAsyncDisposable
{
    private readonly IHost? _ownedHost;
    private readonly MessagingRecorder _recorder;
    private readonly MessagingHarnessOptions _options;
    private bool _disposed;

    private MessagingTestHarness(IServiceProvider services, MessagingRecorder recorder, MessagingHarnessOptions options, IHost? ownedHost)
    {
        Services = services;
        _recorder = recorder;
        _options = options;
        _ownedHost = ownedHost;
        Bus = services.GetRequiredService<IEventBus>();
        Control = services.GetService<IBusControl>();
    }

    /// <summary>The host's service provider — for resolving whatever else the assertion needs (<c>IDeadLetterRepository</c>, a fake, the handler's own state).</summary>
    public IServiceProvider Services { get; }

    /// <summary>The bus under test.</summary>
    public IEventBus Bus { get; }

    /// <summary>Runtime control over the consume side — pause, resume, stop. <c>null</c> only when attached to a host with no messaging transport registered.</summary>
    public IBusControl? Control { get; }

    /// <summary>The observer behind the logs — for registering it a second time elsewhere, or for <see cref="MessagingRecorder.Reset"/>.</summary>
    public MessagingRecorder Recorder => _recorder;

    /// <summary>Envelopes the transport accepted.</summary>
    public RecordedMessageTracker Published => _recorder.Published;

    /// <summary>Sends the transport threw on.</summary>
    public RecordedMessageTracker PublishFaults => _recorder.PublishFaults;

    /// <summary>Delivery attempts that completed, each carrying its <see cref="ConsumeOutcome"/> — one entry per attempt, so a duplicate and a redelivery are both visible.</summary>
    public RecordedMessageTracker Consumed => _recorder.Consumed;

    /// <summary>Delivery attempts that threw — the count is the retry budget actually spent, which is how a test tells a classified-fatal fault from an exhausted one.</summary>
    public RecordedMessageTracker Faulted => _recorder.Faulted;

    /// <summary>Messages that were dead-lettered. Recorded after settlement, so the dead-letter store is already written when a wait on this returns.</summary>
    public RecordedMessageTracker DeadLettered => _recorder.DeadLettered;

    /// <summary>Messages currently in the pipeline. Messages parked at a shut pause gate are not counted — they never entered it.</summary>
    public int InFlight => Control?.InFlight ?? 0;

    /// <summary>
    /// Build and start an in-memory bus with the recorder attached — the one-call setup.
    /// </summary>
    /// <param name="configureServices">Runs after the bus is registered: handlers' collaborators, consume filters, fault classification, concurrency. Replacing a <c>TryAdd</c>ed default needs <c>services.Replace(...)</c>.</param>
    /// <param name="configureBus">In-memory transport options — channel capacity and the retry schedule.</param>
    /// <param name="handlerAssemblies">Assemblies scanned for <see cref="IEventHandler{TEvent}"/>; defaults to the calling assembly.</param>
    /// <param name="options">Harness timings. Defaults to <see cref="MessagingHarnessOptions"/>'s own defaults.</param>
    /// <param name="cancellationToken">Cancellation token for host startup.</param>
    /// <returns>A started harness. Dispose it (<c>await using</c>) to stop the host.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)] // GetCallingAssembly below must see the test, not an inlined caller
    public static Task<MessagingTestHarness> StartAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<InMemoryEventBusOptions>? configureBus = null,
        Assembly[]? handlerAssemblies = null,
        MessagingHarnessOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        return StartCoreAsync(configureServices, configureBus, assemblies, options ?? new MessagingHarnessOptions(), cancellationToken);
    }

    /// <summary>
    /// Wrap a host the test built itself — a <c>WebApplicationFactory</c>, a broker-backed host, anything whose
    /// services already carry a <see cref="MessagingRecorder"/> (register one with <c>AddMessagingRecorder()</c>).
    /// </summary>
    /// <param name="services">The running host's service provider.</param>
    /// <param name="options">Harness timings. Raise <see cref="MessagingHarnessOptions.QuietPeriod"/> for a broker.</param>
    /// <returns>A harness over that host. Disposing it does not stop the host — the test still owns it.</returns>
    /// <exception cref="InvalidOperationException">No <see cref="MessagingRecorder"/> is registered.</exception>
    public static MessagingTestHarness Attach(IServiceProvider services, MessagingHarnessOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var recorder = services.GetService<MessagingRecorder>()
            ?? throw new InvalidOperationException(
                $"No {nameof(MessagingRecorder)} is registered. Call services.{nameof(MessagingTestingServiceCollectionExtensions.AddMessagingRecorder)}() when building the host, or use {nameof(StartAsync)} to build one.");

        return new MessagingTestHarness(services, recorder, options ?? new MessagingHarnessOptions(), ownedHost: null);
    }

    /// <summary>
    /// Wait until the bus has been silent for <paramref name="quietPeriod"/> and nothing is in flight.
    /// </summary>
    /// <param name="quietPeriod">How long the silence must last. Defaults to <see cref="MessagingHarnessOptions.QuietPeriod"/>.</param>
    /// <param name="timeout">Overall budget. Defaults to <see cref="MessagingHarnessOptions.Timeout"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">The bus never went quiet within the budget — the message names what was still moving.</exception>
    /// <remarks>
    ///   - any observed move restarts the quiet window
    ///   - a handler still running keeps the wait going, even with no bus activity
    /// </remarks>
    public async Task WaitForIdleAsync(TimeSpan? quietPeriod = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var quiet = quietPeriod ?? _options.QuietPeriod;
        var budget = timeout ?? _options.Timeout;
        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Captured before the reads below — a move landing in between completes this task and the wait re-evaluates.
            var activity = _recorder.NextActivityAsync();
            var sinceLast = _recorder.SinceLastActivity;
            var inFlight = InFlight;

            if (sinceLast >= quiet && inFlight == 0)
                return;

            var remaining = budget - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException(
                    $"Bus was not idle within {budget}: {inFlight} message(s) in flight, last activity {sinceLast.TotalMilliseconds:F0}ms ago (quiet period {quiet.TotalMilliseconds:F0}ms). {_recorder}");

            // Silence too short → wait out the window; silent but still working → poll the unsignalled in-flight count.
            var wait = sinceLast >= quiet ? _options.PollInterval : quiet - sinceLast;
            if (wait > remaining)
                wait = remaining;

            try
            {
                await activity.WaitAsync(wait > TimeSpan.Zero ? wait : _options.PollInterval, cancellationToken);
            }
            catch (TimeoutException)
            {
                // No move inside the window — that is the outcome this loop is looking for, not a failure.
            }
        }
    }

    /// <summary>Wait until nothing is in flight, ignoring how recently the bus was active.</summary>
    /// <param name="timeout">Overall budget. Defaults to <see cref="MessagingHarnessOptions.Timeout"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">Messages were still in flight when the budget ran out.</exception>
    /// <remarks>
    ///   - reports that handlers finished, never that no more work is coming
    ///   - use only after pausing or stopping the bus, where no arrival is possible
    ///   - prefer <see cref="WaitForIdleAsync"/> otherwise
    /// </remarks>
    public async Task WaitForInFlightZeroAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var budget = timeout ?? _options.Timeout;
        var started = Stopwatch.GetTimestamp();

        while (InFlight != 0)
        {
            if (Stopwatch.GetElapsedTime(started) >= budget)
                throw new TimeoutException($"{InFlight} message(s) still in flight after {budget}. {_recorder}");

            await Task.Delay(_options.PollInterval, cancellationToken);
        }
    }

    /// <summary>Empty every log and restart the activity clock — for a second phase of the same test.</summary>
    public void Reset() => _recorder.Reset();

    /// <summary>Stop and dispose the host, when this harness built it. A harness from <see cref="Attach"/> owns nothing and disposing it is a no-op.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed || _ownedHost is null)
            return;

        _disposed = true;

        try
        {
            await _ownedHost.StopAsync();
        }
        catch (OperationCanceledException)
        {
            // The drain outran the host's shutdown budget; the host still has to be disposed.
        }

        _ownedHost.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<MessagingTestHarness> StartCoreAsync(
        Action<IServiceCollection>? configureServices,
        Action<InMemoryEventBusOptions>? configureBus,
        Assembly[] handlerAssemblies,
        MessagingHarnessOptions options,
        CancellationToken cancellationToken)
    {
        var recorder = new MessagingRecorder();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInMemoryEventBus(configureBus, handlerAssemblies);
        builder.Services.AddMessagingRecorder(recorder); // before configureServices, so a caller's own AddMessagingRecorder() no-ops on ours
        configureServices?.Invoke(builder.Services);

        var host = builder.Build();
        try
        {
            await host.StartAsync(cancellationToken);
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return new MessagingTestHarness(host.Services, recorder, options, host);
    }
}
