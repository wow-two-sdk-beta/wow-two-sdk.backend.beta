using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.S3;

/// <summary>S3-compatible blob storage registration.</summary>
public static class S3BlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IBlobRepository"/> over one S3-compatible bucket, with its own <see cref="IAmazonS3"/> client,
    /// and <see cref="IBlobUrlIssuer"/> presigning URLs for direct uploads and downloads.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Bucket, endpoint or region, and optional static credentials.</param>
    public static IServiceCollection AddS3BlobStorage(this IServiceCollection services, Action<S3BlobStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.BucketName), "S3BlobStorageOptions.BucketName must not be empty.")
                .Validate(o => o.ServiceUrl is not null || !string.IsNullOrWhiteSpace(o.Region), "S3BlobStorageOptions needs a Region or a ServiceUrl.")
                .Validate(o => string.IsNullOrWhiteSpace(o.AccessKey) == string.IsNullOrWhiteSpace(o.SecretKey), "S3BlobStorageOptions.AccessKey and SecretKey go together."));
        // Presigned URLs otherwise fall back to SigV2 for us-east-1 and custom endpoints, which new buckets and R2 reject.
        AWSConfigsS3.UseSignatureVersion4 = true;
        services.TryAddSingleton<IAmazonS3>(serviceProvider => CreateClient(serviceProvider.GetRequiredService<S3BlobStorageOptions>()));
        services.TryAddSingleton<IBlobRepository, S3BlobRepository>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IBlobUrlIssuer, S3BlobUrlIssuer>();
        return services;
    }

    private static AmazonS3Client CreateClient(S3BlobStorageOptions options)
    {
        var config = new AmazonS3Config();
        if (options.ServiceUrl is not null)
        {
            config.ServiceURL = options.ServiceUrl.ToString();
            config.ForcePathStyle = true;
            config.AuthenticationRegion = options.Region ?? "us-east-1";
            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
            config.ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        return string.IsNullOrWhiteSpace(options.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }
}
