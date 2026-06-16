using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

/// <summary>
/// Pipeline composition: behaviors wrap the handler, run in registration order (first registered = outermost),
/// and a short-circuiting behavior can skip the handler by not invoking <c>next</c>.
/// </summary>
public sealed class PipelineBehaviorTests
{
    // Shared ordered sink — each behavior records its label on the way in and on the way out.
    private static readonly AsyncLocal<List<string>> _trace = new();

    private sealed record Probe(string Input) : IRequest<string>;

    private sealed class ProbeHandler : IRequestHandler<Probe, string>
    {
        public ValueTask<string> HandleAsync(Probe request, CancellationToken cancellationToken)
        {
            _trace.Value!.Add("handler");
            return ValueTask.FromResult($"handled:{request.Input}");
        }
    }

    // Position-recording behaviors. Three distinct closed types so DI keeps them as separate registrations.
    private abstract class TracingBehavior<TRequest, TResponse>(string label) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _trace.Value!.Add($"{label}:before");
            var response = await next().ConfigureAwait(false); // await-once
            _trace.Value!.Add($"{label}:after");
            return response;
        }
    }

    private sealed class BehaviorOne<TRequest, TResponse>() : TracingBehavior<TRequest, TResponse>("one") where TRequest : notnull;

    private sealed class BehaviorTwo<TRequest, TResponse>() : TracingBehavior<TRequest, TResponse>("two") where TRequest : notnull;

    private sealed class BehaviorThree<TRequest, TResponse>() : TracingBehavior<TRequest, TResponse>("three") where TRequest : notnull;

    // A behavior that short-circuits — never calls next, returns a canned value.
    private sealed class ShortCircuitBehavior<TRequest, TResponse>(TResponse canned) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _trace.Value!.Add("short-circuit");
            return ValueTask.FromResult(canned); // handler never reached
        }
    }

    private static ISender BuildSender(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection().AddMediator(typeof(PipelineBehaviorTests).Assembly);
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Single_behavior_wraps_the_handler()
    {
        _trace.Value = [];
        var sender = BuildSender(s => s.AddMediatorBehavior(typeof(BehaviorOne<,>)));

        var result = await sender.SendAsync(new Probe("x"));

        result.Should().Be("handled:x");
        _trace.Value.Should().Equal("one:before", "handler", "one:after");
    }

    [Fact]
    public async Task Behaviors_execute_in_registration_order_outermost_first()
    {
        _trace.Value = [];
        var sender = BuildSender(s => s
            .AddMediatorBehavior(typeof(BehaviorOne<,>))    // outermost
            .AddMediatorBehavior(typeof(BehaviorTwo<,>))
            .AddMediatorBehavior(typeof(BehaviorThree<,>))); // innermost

        await sender.SendAsync(new Probe("x"));

        // First registered wraps the rest: one → two → three → handler → three → two → one.
        _trace.Value.Should().Equal(
            "one:before", "two:before", "three:before",
            "handler",
            "three:after", "two:after", "one:after");
    }

    [Fact]
    public async Task A_short_circuiting_behavior_skips_the_handler_and_inner_behaviors()
    {
        _trace.Value = [];

        // Short-circuit is registered first (outermost) with a canned value; BehaviorOne sits inside it.
        // The canned TResponse is supplied via a closed registration so DI can construct the behavior.
        var sender = BuildSender(s =>
        {
            s.AddTransient<IPipelineBehavior<Probe, string>>(_ => new ShortCircuitBehavior<Probe, string>("canned"));
            s.AddMediatorBehavior(typeof(BehaviorOne<,>));
        });

        var result = await sender.SendAsync(new Probe("ignored"));

        result.Should().Be("canned");
        _trace.Value.Should().Equal("short-circuit"); // neither BehaviorOne nor the handler ran
    }

    [Fact]
    public void AddMediatorBehavior_rejects_a_closed_generic_type()
    {
        var services = new ServiceCollection();
        var act = () => services.AddMediatorBehavior(typeof(BehaviorOne<Probe, string>));

        act.Should().Throw<ArgumentException>();
    }
}
