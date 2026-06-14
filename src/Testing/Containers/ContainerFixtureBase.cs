using DotNet.Testcontainers.Containers;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers;

/// <summary>
/// Abstract base for Testcontainers-backed fixtures. Per-engine packages
/// (`Postgres`, `Redis`, `RabbitMq`, ...) inherit and supply the concrete container.
/// </summary>
/// <typeparam name="TContainer">The concrete Testcontainers container type.</typeparam>
public abstract class ContainerFixtureBase<TContainer> : IAsyncTestFixture
    where TContainer : IContainer
{
    /// <summary>The underlying container. Built once on first <see cref="StartAsync"/>.</summary>
    protected TContainer Container { get; }

    /// <summary>Whether <see cref="StartAsync"/> has run successfully.</summary>
    public bool IsStarted { get; private set; }

    /// <summary>Stable identifier (e.g. "postgres").</summary>
    public abstract string Name { get; }

    /// <summary>Constructor receives the pre-built container instance from the derived class.</summary>
    protected ContainerFixtureBase(TContainer container) => Container = container;

    /// <inheritdoc />
    public virtual async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsStarted) return;
        await Container.StartAsync(cancellationToken).ConfigureAwait(false);
        IsStarted = true;
    }

    /// <inheritdoc />
    public virtual ValueTask ResetAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask; // override in derived to wipe state

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
