using System.Net;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Models;

namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Brokers;

/// <summary>Defines resolving where an IP address is registered.</summary>
/// <remarks>Lookups are in-memory and synchronous, so a request path never waits on a remote geolocation call.</remarks>
public interface IIpLocationBroker
{
    /// <summary>Locates <paramref name="address"/>.</summary>
    /// <param name="address">The client address, after trusted proxy headers are applied.</param>
    /// <returns>The location, or <see langword="null"/> for private, reserved and unknown addresses.</returns>
    IpLocationModel? Locate(IPAddress address);
}
