using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Outcome of a single webhook send attempt.</summary>
internal enum SendOutcome
{
    /// <summary>2xx response.</summary>
    Success,

    /// <summary>Retryable failure — 5xx, 408, timeout, or connection error.</summary>
    Transient,

    /// <summary>Non-retryable failure — any other 4xx.</summary>
    Permanent,
}
