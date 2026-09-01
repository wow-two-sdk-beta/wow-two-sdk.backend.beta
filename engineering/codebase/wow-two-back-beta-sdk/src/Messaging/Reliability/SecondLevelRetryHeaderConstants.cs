using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Reserved wire header carrying a message's position in the second-level retry ladder.</summary>
public static class SecondLevelRetryHeaderConstants
{
    /// <summary>Reserved. How many second-level tiers this message has already been promoted through; absent means none.</summary>
    public const string Tier = MessageHeaderConstants.ReservedPrefix + "retry-tier";

    /// <summary>Read the tier marker off an envelope; 0 when absent or unparseable.</summary>
    /// <param name="envelope">The envelope to inspect.</param>
    public static int ReadTier(EventEnvelope? envelope)
    {
        if (envelope?.Headers is not { Count: > 0 } headers || !headers.TryGetValue(Tier, out var raw))
            return 0;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier) && tier > 0 ? tier : 0;
    }
}
