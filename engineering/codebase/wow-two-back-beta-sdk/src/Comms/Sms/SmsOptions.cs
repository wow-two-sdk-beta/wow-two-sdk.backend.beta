namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Holds the cross-provider SMS defaults: the sender and the broker a caller gets when it names none.</summary>
/// <remarks>Set in code with <c>AddSmsDefaults(o => …)</c> or in the host section <c>Comms:Sms</c>, which is applied last.</remarks>
public sealed record SmsOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Comms:Sms";

    /// <summary>Gets or sets the sender id or number used when a message names none; null leaves the provider's default.</summary>
    public string? DefaultFrom { get; set; }

    /// <summary>Gets or sets the broker name <see cref="ISmsBrokerFactory"/> returns for no name, such as <c>eskiz</c>; null takes the first registered.</summary>
    public string? DefaultBroker { get; set; }
}
