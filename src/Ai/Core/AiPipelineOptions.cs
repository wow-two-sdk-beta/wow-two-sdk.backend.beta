namespace WoW.Two.Sdk.Backend.Beta.Ai.Core;

/// <summary>
/// Conventional middleware toggles applied by the SDK around every provider's <c>IChatClient</c>
/// (function/tool invocation and OpenTelemetry). Providers accept this so the pipeline is uniform
/// regardless of which broker backs it.
/// </summary>
public sealed class AiPipelineOptions
{
    /// <summary>Gets or sets whether automatic function (tool) invocation is enabled. Default <see langword="true"/>.</summary>
    public bool EnableFunctionInvocation { get; set; } = true;

    /// <summary>Gets or sets whether OpenTelemetry chat instrumentation is enabled (rides the SDK's OTel setup). Default <see langword="true"/>.</summary>
    public bool EnableTelemetry { get; set; } = true;
}
