using System.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>Diagnostics for the messaging layer — the OpenTelemetry <see cref="ActivitySource"/> and W3C trace-context header keys.</summary>
internal static class MessagingDiagnostics
{
    /// <summary>The messaging ActivitySource name. Add it to the tracer via <c>AddOpenTelemetryTracing</c> (auto) or <c>tracing.AddSource(...)</c>.</summary>
    public const string ActivitySourceName = "WoW.Two.Messaging";

    /// <summary>W3C <c>traceparent</c> header key on the envelope.</summary>
    public const string TraceParentHeader = "traceparent";

    /// <summary>W3C <c>tracestate</c> header key on the envelope.</summary>
    public const string TraceStateHeader = "tracestate";

    /// <summary>The messaging ActivitySource.</summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);
}
