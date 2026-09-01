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

/// <summary>Reserved wire headers carrying a message's claim check — the pointer to a body held in blob storage rather than on the wire.</summary>
public static class ClaimCheckHeaderConstants
{
    /// <summary>Reserved. Logical blob path of the offloaded body. Its presence is what marks a message as claim-checked.</summary>
    public const string Reference = MessageHeaderConstants.ReservedPrefix + "claim-check";

    /// <summary>Reserved. Size in bytes of the offloaded body, for observability and for spotting a truncated blob.</summary>
    public const string Size = MessageHeaderConstants.ReservedPrefix + "claim-check-size";

    /// <summary>Reserved. Wire token of the <i>real</i> body type — the one the offloaded blob deserializes into.</summary>
    public const string BodyType = MessageHeaderConstants.ReservedPrefix + "claim-check-type";

    /// <summary>Read the claim reference off an envelope.</summary>
    /// <param name="envelope">The envelope to inspect.</param>
    /// <param name="path">The blob path, when the message carries one.</param>
    /// <returns><see langword="true"/> when the body is offloaded and must be rehydrated before dispatch.</returns>
    public static bool TryReadReference(EventEnvelope? envelope, out string path)
    {
        if (envelope?.Headers is { Count: > 0 } headers
            && headers.TryGetValue(Reference, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            path = value;
            return true;
        }

        path = string.Empty;
        return false;
    }
}
