using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>A response arrived on the request's conversation, but it is not the expected response contract.</summary>
public sealed class RequestFaultException : Exception
{
    /// <summary>Create the exception.</summary>
    public RequestFaultException()
        : base("The response did not match the expected response contract.")
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    public RequestFaultException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The underlying fault.</param>
    public RequestFaultException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
