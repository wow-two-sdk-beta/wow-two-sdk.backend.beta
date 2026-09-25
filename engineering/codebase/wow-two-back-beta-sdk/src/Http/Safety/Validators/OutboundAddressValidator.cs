using System.Net;
using System.Net.Sockets;

namespace WoW.Two.Sdk.Backend.Beta.Http.Safety.Validators;

/// <summary>Validates outbound IP addresses against local, reserved and transition ranges.</summary>
public sealed class OutboundAddressValidator
{
    /// <summary>Checks whether an address is public unicast under the conservative outbound policy.</summary>
    /// <param name="address">The resolved or literal destination address.</param>
    public bool IsAllowed(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        IPAddress ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        byte[] bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] is 0 or 10 or 127
                || bytes[0] >= 224
                || bytes[0] == 100 && (bytes[1] & 0xC0) == 64
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 172 && (bytes[1] & 0xF0) == 16
                || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2
                || bytes[0] == 192 && bytes[1] == 88 && bytes[2] == 99
                || bytes[0] == 198 && bytes[1] is 18 or 19
                || bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100
                || bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }

        // Permit global unicast only; exclude IETF protocol, documentation and 6to4 ranges.
        return ip.AddressFamily == AddressFamily.InterNetworkV6
            && (bytes[0] & 0xE0) == 0x20
            && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] < 2)
            && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
            && !(bytes[0] == 0x20 && bytes[1] == 0x02)
            && !(bytes[0] == 0x3F && bytes[1] == 0xFF && (bytes[2] & 0xF0) == 0);
    }
}
