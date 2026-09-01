namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Publisher — fire a notification to all registered handlers.</summary>
public interface IPublisher
{
    /// <summary>Publish a notification.</summary>
    /// <param name="notification">The notification to dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
}
