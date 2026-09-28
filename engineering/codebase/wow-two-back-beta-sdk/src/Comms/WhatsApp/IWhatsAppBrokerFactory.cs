namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Defines behavior that creates the WhatsApp broker registered under a name.</summary>
public interface IWhatsAppBrokerFactory
{
    /// <summary>Creates the broker named <paramref name="name"/>, or the default broker for no name.</summary>
    /// <param name="name">The broker name, such as <see cref="WhatsAppBrokerNameConstants.Meta"/>; null or empty takes the default.</param>
    /// <returns>The broker, or <c>null</c> when none is registered under the name.</returns>
    IWhatsAppBroker? Create(string? name = null);
}
