using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.FileSystem;

/// <summary>Registers the local-filesystem <see cref="IBlobStorage"/> implementation.</summary>
public static class LocalBlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="LocalFileBlobStorage"/> as the <see cref="IBlobStorage"/> singleton, rooted at
    /// <paramref name="rootPath"/>. Suitable for development and single-node deployments; swap for a cloud
    /// adapter (S3/Azure/GCS) in production without changing call sites.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="rootPath">The base directory under which blobs are stored.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddLocalBlobStorage(this IServiceCollection services, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        services.TryAddSingleton<IBlobStorage>(_ => new LocalFileBlobStorage(rootPath));
        return services;
    }
}
