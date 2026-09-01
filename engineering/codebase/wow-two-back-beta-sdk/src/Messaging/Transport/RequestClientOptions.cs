using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Process-wide defaults for <see cref="IRequestClient{TRequest, TResponse}"/>.</summary>
public sealed record RequestClientOptions
{
    /// <summary>How long a request waits for its response before <see cref="RequestTimeoutException"/>. Default 30s. <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> waits forever (bounded only by the caller's token).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Address responses are sent to. Null (default) derives it from <see cref="ITopologyService"/> — this process's
    /// own consume endpoint, which the topology already binds by name, so a response addressed to it lands here without
    /// declaring anything new. Set it to point replies at a queue of your own.
    /// </summary>
    public string? ReplyAddress { get; set; }
}
