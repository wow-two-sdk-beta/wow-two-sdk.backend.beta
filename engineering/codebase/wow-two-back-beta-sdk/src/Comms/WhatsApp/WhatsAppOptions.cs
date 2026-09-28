namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Holds the cross-provider WhatsApp defaults: the broker a caller gets when it names none.</summary>
/// <remarks>Set in the host section <c>Comms:WhatsApp</c>; provider sections nest under it.</remarks>
public sealed record WhatsAppOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Comms:WhatsApp";

    /// <summary>Gets or sets the broker name <see cref="IWhatsAppBrokerFactory"/> returns for no name; null takes the first registered.</summary>
    public string? DefaultBroker { get; set; }
}
