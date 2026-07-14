using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ML.Tokenizers;

namespace WoW.Two.Sdk.Backend.Beta.Ai.Tokenizers;

/// <summary>Registers a model-aware <see cref="ITokenCounter"/> for prompt-budget and cost estimation.</summary>
public static class TokenizerServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="ITokenCounter"/> using the tiktoken tokenizer for the given model
    /// (e.g. <c>gpt-4o</c>), as a singleton. Idempotent.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="model">The model name whose tiktoken encoding to use.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddTiktokenTokenCounter(this IServiceCollection services, string model)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var tokenizer = TiktokenTokenizer.CreateForModel(model);
        services.TryAddSingleton<ITokenCounter>(new TiktokenTokenCounter(tokenizer));
        return services;
    }
}
