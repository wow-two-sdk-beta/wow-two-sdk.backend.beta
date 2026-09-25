namespace WoW.Two.Sdk.Backend.Beta.Http.Safety;

/// <summary>Represents rejection of an outbound destination by the configured network policy.</summary>
public sealed class OutboundAddressBlockedException : HttpRequestException
{
    /// <summary>Creates a destination-policy failure without including credentials or URL query data.</summary>
    /// <param name="host">The rejected host.</param>
    public OutboundAddressBlockedException(string host) : base($"Outbound HTTP destination '{host}' is blocked.")
    {
    }
}
