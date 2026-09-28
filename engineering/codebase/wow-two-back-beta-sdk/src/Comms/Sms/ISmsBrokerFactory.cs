namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Defines behavior that creates the SMS broker registered under a name.</summary>
public interface ISmsBrokerFactory
{
    /// <summary>Creates the broker named <paramref name="name"/>, or the default broker for no name.</summary>
    /// <param name="name">The broker name, such as <see cref="SmsBrokerNameConstants.Eskiz"/>; null or empty takes the default.</param>
    /// <returns>The broker, or <c>null</c> when none is registered under the name.</returns>
    ISmsBroker? Create(string? name = null);
}
