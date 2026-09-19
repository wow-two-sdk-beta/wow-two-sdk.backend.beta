using System.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Logging;

/// <summary>Holds the mediator module's OpenTelemetry activity source.</summary>
internal static class MediatorDiagnosticConstants
{
    /// <summary>Holds the stable source name collected by the SDK tracing helper.</summary>
    public const string ActivitySourceName = "WoW.Two.Mediator";

    /// <summary>Holds the long-lived source for request activities.</summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);
}
