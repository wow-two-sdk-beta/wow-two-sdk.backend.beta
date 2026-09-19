using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Holds reserved wire header carrying a message's position in the second-level retry ladder.</summary>
public static class SecondLevelRetryHeaderConstants
{
    /// <summary>Holds the reserved second-level retry-tier header.</summary>
    public const string Tier = MessageHeaderConstants.ReservedPrefix + "retry-tier";

    /// <summary>Read the tier marker off an envelope; 0 when absent or unparseable.</summary>
    /// <param name="envelope">The envelope to inspect.</param>
    public static int ReadTier(EventEnvelopeModel? envelope)
    {
        if (envelope?.Headers is not { Count: > 0 } headers || !headers.TryGetValue(Tier, out var raw))
            return 0;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier) && tier > 0 ? tier : 0;
    }
}
