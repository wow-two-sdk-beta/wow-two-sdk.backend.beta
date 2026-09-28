using global::Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.Azure;

/// <summary>Azure Blob Storage registration.</summary>
public static class AzureBlobStorageServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IBlobRepository"/> over one Azure Blob Storage container.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Container plus a connection string, or a service URI with a credential.</param>
    public static IServiceCollection AddAzureBlobStorage(this IServiceCollection services, Action<AzureBlobStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName), "AzureBlobStorageOptions.ContainerName must not be empty.")
                .Validate(
                    o => !string.IsNullOrWhiteSpace(o.ConnectionString) || (o.ServiceUri is not null && o.Credential is not null),
                    "AzureBlobStorageOptions needs a ConnectionString, or a ServiceUri with a Credential."));
        services.TryAddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<AzureBlobStorageOptions>();
            var service = string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new BlobServiceClient(options.ServiceUri, options.Credential)
                : new BlobServiceClient(options.ConnectionString);
            return service.GetBlobContainerClient(options.ContainerName);
        });
        services.TryAddSingleton<IBlobRepository, AzureBlobRepository>();
        return services;
    }
}
