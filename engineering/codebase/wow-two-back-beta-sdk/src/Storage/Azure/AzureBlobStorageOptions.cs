using Azure.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.Azure;

/// <summary>Holds the Azure Blob Storage container and how to reach it: a connection string, or a service URI with a credential.</summary>
public sealed record AzureBlobStorageOptions
{
    /// <summary>The container every blob lives in.</summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>Account connection string (account key, SAS or Azurite's development string).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Blob service URI (<c>https://{account}.blob.core.windows.net</c>); pair it with <see cref="Credential"/>.</summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>Token credential for <see cref="ServiceUri"/>, such as <c>DefaultAzureCredential</c> from the host's Azure.Identity.</summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>Optional name prefix placing this app's blobs under one folder of a shared container.</summary>
    public string? KeyPrefix { get; set; }

    /// <summary>Create the container on first write when it is missing (development and tests). Default false.</summary>
    public bool CreateContainerIfMissing { get; set; }
}
