using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Provides registration extensions for the tamper-evident hash-chain audit primitives.</summary>
public static class HashChainServiceCollectionExtensions
{
    /// <summary>Registers a hash-chain sealer and verifier for <typeparamref name="TEntry"/>, wiring the consumer's <typeparamref name="TCanonicalizer"/> as the field projection.</summary>
    /// <remarks>The canonicalizer decides which domain fields are hashed; the SDK adds the chain fields. Registers <see cref="IHashChainSealer{TEntry}"/>, <see cref="IHashChainVerifier{TEntry}"/>, and <see cref="IChainedEntryCanonicalizer{TEntry}"/> as singletons (the primitives are stateless).</remarks>
    /// <typeparam name="TEntry">The consumer's entry type exposing the chain fields.</typeparam>
    /// <typeparam name="TCanonicalizer">The consumer projection of the entry's domain fields onto the canonical payload.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of the hash algorithm.</param>
    public static IServiceCollection AddHashChain<TEntry, TCanonicalizer>(
        this IServiceCollection services,
        Action<HashChainOptions>? configure = null)
        where TEntry : IHashChainedEntry
        where TCanonicalizer : class, IChainedEntryCanonicalizer<TEntry>
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<HashChainOptions>();
        }

        services.TryAddSingleton<IChainedEntryCanonicalizer<TEntry>, TCanonicalizer>();
        services.TryAddSingleton<IHashChainSealer<TEntry>, HashChainSealer<TEntry>>();
        services.TryAddSingleton<IHashChainVerifier<TEntry>, HashChainVerifier<TEntry>>();
        return services;
    }

    /// <summary>Registers a hash-chain sealer and verifier for <typeparamref name="TEntry"/> using a pre-built canonicalizer instance.</summary>
    /// <remarks>Use when the canonicalizer needs constructor state the container does not supply. Registers <see cref="IHashChainSealer{TEntry}"/>, <see cref="IHashChainVerifier{TEntry}"/>, and the supplied <see cref="IChainedEntryCanonicalizer{TEntry}"/> as singletons.</remarks>
    /// <typeparam name="TEntry">The consumer's entry type exposing the chain fields.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="canonicalizer">The consumer projection instance to register.</param>
    /// <param name="configure">Optional override of the hash algorithm.</param>
    public static IServiceCollection AddHashChain<TEntry>(
        this IServiceCollection services,
        IChainedEntryCanonicalizer<TEntry> canonicalizer,
        Action<HashChainOptions>? configure = null)
        where TEntry : IHashChainedEntry
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(canonicalizer);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<HashChainOptions>();
        }

        services.TryAddSingleton<IChainedEntryCanonicalizer<TEntry>>(canonicalizer);
        services.TryAddSingleton<IHashChainSealer<TEntry>, HashChainSealer<TEntry>>();
        services.TryAddSingleton<IHashChainVerifier<TEntry>, HashChainVerifier<TEntry>>();
        return services;
    }
}
