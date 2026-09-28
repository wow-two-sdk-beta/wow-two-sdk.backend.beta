namespace WoW.Two.Sdk.Backend.Beta.Storage.S3;

/// <summary>Holds the bucket, endpoint and credentials of an S3-compatible blob store (AWS S3, MinIO, Cloudflare R2, …).</summary>
public sealed record S3BlobStorageOptions
{
    /// <summary>The bucket every blob lives in.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>Optional key prefix placing this app's blobs under one folder of a shared bucket.</summary>
    public string? KeyPrefix { get; set; }

    /// <summary>AWS region (<c>eu-central-1</c>); ignored when <see cref="ServiceUrl"/> is set.</summary>
    public string? Region { get; set; }

    /// <summary>Endpoint of an S3-compatible service; when set, path-style addressing and on-demand checksums apply.</summary>
    public Uri? ServiceUrl { get; set; }

    /// <summary>Access key id; null uses the default AWS credential chain (environment, profile, instance role).</summary>
    public string? AccessKey { get; set; }

    /// <summary>Secret access key paired with <see cref="AccessKey"/>.</summary>
    public string? SecretKey { get; set; }
}
