using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Defines behavior that supplies the address this process wants its responses sent to. Replace it to route replies somewhere other than the
/// process's own consume endpoint — a per-instance queue, a shared reply endpoint, a broker-native inbox.
/// </summary>
public interface IReplyAddressService
{
    /// <summary>The address a responder should send its reply to. Read once per request, so an implementation may vary it.</summary>
    string ReplyAddress { get; }
}
