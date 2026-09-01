using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

/// <summary>
/// Pipeline composition: behaviors wrap the handler, run in registration order (first registered = outermost),
/// and a short-circuiting behavior can skip the handler by not invoking <c>nextStep</c>.
/// </summary>
public sealed class PipelineBehaviorTests
{
    // Shared ordered sink — each behavior records its label on the way in and on the way out.
    private static readonly AsyncLocal<List<string>> Trace = new();

    private sealed record Probe(string Input) : IRequest<string>;

    private sealed class ProbeHandler : IRequestHandler<Probe, string>
    {
        public ValueTask<string> HandleAsync(Probe request, CancellationToken cancellationToken)
        {
            Trace.Value!.Add("handler");
            return ValueTask.FromResult($"handled:{request.Input}");
        }
    }

    // Position-recording behaviors. Three distinct closed types so DI keeps them as separate registrations.
    private abstract class TracingInterceptor<TRequest, TResponse>(string label) : IRequestInterceptor<TRequest, TResponse>
        where TRequest : notnull
    {
        public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
        {
            Trace.Value!.Add($"{label}:before");
            var response = await nextStep().ConfigureAwait(false); // await-once
            Trace.Value!.Add($"{label}:after");
            return response;
        }
    }

    private sealed class BehaviorOne<TRequest, TResponse>() : TracingInterceptor<TRequest, TResponse>("one") where TRequest : notnull;

    private sealed class BehaviorTwo<TRequest, TResponse>() : TracingInterceptor<TRequest, TResponse>("two") where TRequest : notnull;

    private sealed class BehaviorThree<TRequest, TResponse>() : TracingInterceptor<TRequest, TResponse>("three") where TRequest : notnull;

    // A behavior that short-circuits — never calls nextStep, returns a canned value.
    private sealed class ShortCircuitingInterceptor<TRequest, TResponse>(TResponse canned) : IRequestInterceptor<TRequest, TResponse>
        where TRequest : notnull
    {
        public ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
        {
            Trace.Value!.Add("short-circuit");
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
    public async Task SingleBehavior_ShouldWrapTheHandler()
    {
        Trace.Value = [];
        var sender = BuildSender(s => s.AddMediatorInterceptor(typeof(BehaviorOne<,>)));

        var result = await sender.SendAsync(new Probe("x"));

        result.Should().Be("handled:x");
        Trace.Value.Should().Equal("one:before", "handler", "one:after");
    }

    [Fact]
    public async Task Behaviors_ShouldExecuteInRegistrationOrder_OutermostFirst()
    {
        Trace.Value = [];
        var sender = BuildSender(s => s
            .AddMediatorInterceptor(typeof(BehaviorOne<,>))    // outermost
            .AddMediatorInterceptor(typeof(BehaviorTwo<,>))
            .AddMediatorInterceptor(typeof(BehaviorThree<,>))); // innermost

        await sender.SendAsync(new Probe("x"));

        // First registered wraps the rest: one → two → three → handler → three → two → one.
        Trace.Value.Should().Equal(
            "one:before", "two:before", "three:before",
            "handler",
            "three:after", "two:after", "one:after");
    }

    [Fact]
    public async Task ShortCircuitingBehavior_ShouldSkipTheHandlerAndInnerBehaviors()
    {
        Trace.Value = [];

        // Short-circuit is registered first (outermost) with a canned value; BehaviorOne sits inside it.
        // The canned TResponse is supplied via a closed registration so DI can construct the behavior.
        var sender = BuildSender(s =>
        {
            s.AddTransient<IRequestInterceptor<Probe, string>>(_ => new ShortCircuitingInterceptor<Probe, string>("canned"));
            s.AddMediatorInterceptor(typeof(BehaviorOne<,>));
        });

        var result = await sender.SendAsync(new Probe("ignored"));

        result.Should().Be("canned");
        Trace.Value.Should().Equal("short-circuit"); // neither BehaviorOne nor the handler ran
    }

    [Fact]
    public void AddMediatorBehavior_ShouldReject_WhenClosedGenericType()
    {
        var services = new ServiceCollection();
        var act = () => services.AddMediatorInterceptor(typeof(BehaviorOne<Probe, string>));

        act.Should().Throw<ArgumentException>();
    }
}
