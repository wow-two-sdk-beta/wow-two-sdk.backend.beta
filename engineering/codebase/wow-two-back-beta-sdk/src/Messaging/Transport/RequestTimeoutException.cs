using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>No response arrived for a request within its timeout. The pending request has already been released — the exchange is over, not merely late.</summary>
public sealed class RequestTimeoutException : Exception
{
    /// <summary>Create the exception.</summary>
    public RequestTimeoutException()
        : base("The request timed out before a response arrived.")
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    public RequestTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The underlying timeout.</param>
    public RequestTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
