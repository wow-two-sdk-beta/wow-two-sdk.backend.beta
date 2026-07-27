using Microsoft.Extensions.AI;

namespace WoW.Two.Sdk.Backend.Beta.Ai.Core;

/// <summary>
/// Applies the SDK's conventional <c>IChatClient</c> pipeline — function/tool invocation and OpenTelemetry
/// — around a provider's raw client. Every provider broker terminates its builder with <see cref="Apply"/>
/// so consumers get a uniform, observable, tool-capable client no matter the backend.
/// </summary>
public static class ChatClientConventions
{
    /// <summary>
    /// Wraps <paramref name="innerClient"/> with the conventional middleware selected by
    /// <paramref name="options"/> and returns the built <see cref="IChatClient"/>. Terminal call on a
    /// <see cref="ChatClientBuilder"/>.
    /// </summary>
    /// <param name="builder">The chat-client builder (from <c>AddChatClient</c>).</param>
    /// <param name="innerClient">The provider's raw chat client to wrap.</param>
    /// <param name="options">Middleware toggles; defaults enable function invocation + telemetry.</param>
    /// <returns>The composed chat client.</returns>
    public static IChatClient Apply(this ChatClientBuilder builder, IChatClient innerClient, AiPipelineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(innerClient);

        options ??= new AiPipelineOptions();

        if (options.EnableFunctionInvocation)
            builder = builder.UseFunctionInvocation();

        if (options.EnableTelemetry)
            builder = builder.UseOpenTelemetry();

        return builder.Use(innerClient);
    }
}
