namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Defines behavior that creates the delivery handler registered for a channel name.</summary>
public interface IOtpDeliveryHandlerFactory
{
    /// <summary>Creates the handler for <paramref name="channel"/>.</summary>
    /// <param name="channel">The channel name, such as <see cref="OtpChannelNameConstants.Sms"/>.</param>
    /// <returns>The handler, or <c>null</c> when no handler is registered for the channel.</returns>
    IOtpDeliveryHandler? Create(string channel);
}
