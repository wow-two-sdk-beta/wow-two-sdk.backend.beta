using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using WoW.Two.Sdk.Backend.Beta.Ai.Core;

namespace WoW.Two.Sdk.Backend.Beta.Ai.Ollama;

/// <summary>Registers an Ollama-backed <c>IChatClient</c>/<c>IEmbeddingGenerator</c> — local models via the Ollama server.</summary>
public static class OllamaChatClientServiceCollectionExtensions
{
    /// <summary>Registers an <see cref="IChatClient"/> backed by a local Ollama model, wrapped in the SDK pipeline.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="endpoint">The Ollama server endpoint (e.g. <c>http://localhost:11434</c>).</param>
    /// <param name="model">The default model tag (e.g. <c>llama3.2</c>).</param>
    /// <param name="configure">Optional pipeline overrides (function invocation, telemetry).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOllamaChatClient(this IServiceCollection services, Uri endpoint, string model, Action<AiPipelineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var options = new AiPipelineOptions();
        configure?.Invoke(options);

        services.AddChatClient(builder => builder.Apply(new OllamaApiClient(endpoint, model), options));
        return services;
    }

    /// <summary>Registers an Ollama <see cref="IChatClient"/> from a string endpoint.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="endpoint">The Ollama server endpoint URL.</param>
    /// <param name="model">The default model tag.</param>
    /// <param name="configure">Optional pipeline overrides.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOllamaChatClient(this IServiceCollection services, string endpoint, string model, Action<AiPipelineOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        return services.AddOllamaChatClient(new Uri(endpoint), model, configure);
    }

    /// <summary>Registers an <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> backed by a local Ollama embedding model.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="endpoint">The Ollama server endpoint.</param>
    /// <param name="model">The embedding model tag (e.g. <c>nomic-embed-text</c>).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOllamaEmbeddingGenerator(this IServiceCollection services, Uri endpoint, string model)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.AddEmbeddingGenerator<string, Embedding<float>>(builder => builder.Use(new OllamaApiClient(endpoint, model)));
        return services;
    }
}
