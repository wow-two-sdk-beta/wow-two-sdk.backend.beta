using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Default saga runner — resolves each step from a fresh DI scope, runs steps in order, and on failure
/// (faulted outcome or thrown exception) compensates the completed steps in reverse. The compensation stack
/// is the Saga Execution Coordinator.
/// </summary>
public sealed partial class SagaRunner(IServiceScopeFactory scopeFactory, ILogger<SagaRunner> logger) : ISagaRunner
{
    /// <inheritdoc />
    public async ValueTask<SagaResult> RunAsync(SagaDefinition definition, SagaContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var completed = new Stack<ISagaStep>();

        foreach (var stepType in definition.StepTypes)
        {
            var step = (ISagaStep)services.GetRequiredService(stepType);
            SagaStepOutcome outcome;
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
                outcome = SagaStepOutcome.Faulted(ex.Message);
            }

            if (outcome.Succeeded)
            {
                completed.Push(step);
                continue;
            }

            LogStepFailing(definition.Name, step.Name, outcome.FailureReason);
            var compensated = await CompensateAsync(definition, completed, context, cancellationToken);
            return new SagaResult(false, step.Name, outcome.FailureReason, compensated);
        }

        return new SagaResult(true, null, null, Array.Empty<string>());
    }

    private async ValueTask<IReadOnlyList<string>> CompensateAsync(SagaDefinition definition, Stack<ISagaStep> completed, SagaContext context, CancellationToken cancellationToken)
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

/// <summary>In-process <see cref="ISagaTransport"/> — routes step-emitted messages through the in-memory <see cref="IMessageBus"/>.</summary>
internal sealed class InProcessSagaTransport(IMessageBus bus) : ISagaTransport
{
    public ValueTask SendAsync<TMessage>(string destination, TMessage message, SagaContext context, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(context);
        return bus.SendAsync(destination, message, new SendOptions { CorrelationId = context.CorrelationId }, cancellationToken);
    }
}
