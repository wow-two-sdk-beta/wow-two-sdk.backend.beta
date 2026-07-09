using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>
/// Default event-saga runner — resolves each step from a fresh DI scope, runs steps in order, and on failure
/// (faulted outcome or thrown exception) compensates the completed steps in reverse. The compensation stack
/// is the Saga Execution Coordinator.
/// </summary>
public sealed partial class EventSagaRunner(IServiceScopeFactory scopeFactory, ILogger<EventSagaRunner> logger) : IEventSagaRunner
{
    /// <inheritdoc />
    public async ValueTask<EventSagaResult> RunAsync(EventSagaDefinition definition, EventSagaContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var completed = new Stack<IEventSagaStep>();

        foreach (var stepType in definition.StepTypes)
        {
            var step = (IEventSagaStep)services.GetRequiredService(stepType);
            EventSagaStepOutcome outcome;
            try
            {
                outcome = await step.ExecuteAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogStepThrew(ex, definition.Name, step.Name);
                outcome = EventSagaStepOutcome.Faulted(ex.Message);
            }

            if (outcome.Succeeded)
            {
                completed.Push(step);
                continue;
            }

            LogStepFailing(definition.Name, step.Name, outcome.FailureReason);
            var compensated = await CompensateAsync(definition, completed, context, cancellationToken);
            return new EventSagaResult(false, step.Name, outcome.FailureReason, compensated);
        }

        return new EventSagaResult(true, null, null, Array.Empty<string>());
    }

    private async ValueTask<IReadOnlyList<string>> CompensateAsync(EventSagaDefinition definition, Stack<IEventSagaStep> completed, EventSagaContext context, CancellationToken cancellationToken)
    {
        var compensated = new List<string>();
        while (completed.Count > 0)
        {
            var step = completed.Pop();
            try
            {
                await step.CompensateAsync(context, cancellationToken);
                compensated.Add(step.Name);
            }
            catch (Exception ex)
            {
                LogCompensationFailed(ex, definition.Name, step.Name);
            }
        }

        return compensated;
    }

    [LoggerMessage(EventId = 6101, Level = LogLevel.Error, Message = "Saga {Saga} step {Step} threw")]
    private partial void LogStepThrew(Exception exception, string saga, string step);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Warning, Message = "Saga {Saga} failing at step {Step}: {Reason}")]
    private partial void LogStepFailing(string saga, string step, string? reason);

    [LoggerMessage(EventId = 6103, Level = LogLevel.Error, Message = "Saga {Saga} compensation for step {Step} failed")]
    private partial void LogCompensationFailed(Exception exception, string saga, string step);
}

/// <summary>In-process <see cref="IEventSagaTransport"/> — routes step-emitted events through the in-memory <see cref="IEventBus"/>.</summary>
internal sealed class InProcessEventSagaTransport(IEventBus bus) : IEventSagaTransport
{
    public ValueTask SendAsync<TEvent>(string destination, TEvent @event, EventSagaContext context, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        return bus.SendAsync(destination, @event, new SendOptions { CorrelationId = context.CorrelationId }, cancellationToken);
    }
}
