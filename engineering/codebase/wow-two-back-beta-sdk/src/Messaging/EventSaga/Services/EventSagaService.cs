using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga.Services;

/// <summary>
/// Provides ordered event-saga execution in one DI scope per run, shared by all steps. On failure
/// (faulted outcome or thrown exception), compensates completed steps in reverse within that scope.
/// </summary>
public sealed partial class EventSagaService(IServiceScopeFactory scopeFactory, ILogger<EventSagaService> logger) : IEventSagaService
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
            return new EventSagaResult { Succeeded = false, FailedStep = step.Name, FailureReason = outcome.FailureReason, CompensatedSteps = compensated };
        }

        return new EventSagaResult { Succeeded = true, FailedStep = null, FailureReason = null, CompensatedSteps = Array.Empty<string>() };
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
