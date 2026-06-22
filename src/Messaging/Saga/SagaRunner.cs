using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Default saga runner — resolves each step from a fresh DI scope, runs steps in order, and on failure
/// (faulted outcome or thrown exception) compensates the completed steps in reverse. The compensation stack
/// is the Saga Execution Coordinator.
/// </summary>
public sealed class SagaRunner(IServiceScopeFactory scopeFactory, ILogger<SagaRunner> logger) : ISagaRunner
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
                logger.LogError(ex, "Saga {Saga} step {Step} threw", definition.Name, step.Name);
                outcome = SagaStepOutcome.Faulted(ex.Message);
            }

            if (outcome.Succeeded)
            {
                completed.Push(step);
                continue;
            }

            logger.LogWarning("Saga {Saga} failing at step {Step}: {Reason}", definition.Name, step.Name, outcome.FailureReason);
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
                logger.LogError(ex, "Saga {Saga} compensation for step {Step} failed", definition.Name, step.Name);
            }
        }

        return compensated;
    }
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
