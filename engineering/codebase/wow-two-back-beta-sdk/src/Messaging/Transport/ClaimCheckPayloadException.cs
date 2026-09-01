using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// An offloaded body could not be read back, so the message cannot be handled — the blob is gone, unreadable, or the
/// reference does not point at something this consumer is willing to fetch. Terminal: a redelivery reads the same
/// reference and fails the same way.
/// </summary>
public sealed class ClaimCheckPayloadException : Exception
{
    /// <summary>Create the exception.</summary>
    public ClaimCheckPayloadException()
        : base("The claim-checked body could not be read back from blob storage.")
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message, which becomes the dead-letter reason.</param>
    public ClaimCheckPayloadException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message, which becomes the dead-letter reason.</param>
    /// <param name="innerException">The underlying storage or deserialization failure.</param>
    public ClaimCheckPayloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
