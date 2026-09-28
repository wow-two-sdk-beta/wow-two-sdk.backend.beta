# Storage.Azure

`AzureBlobRepository` — `IBlobRepository` over one Azure Blob Storage container (or Azurite in development).

```csharp
// connection string (account key, SAS, or Azurite)
services.AddAzureBlobStorage(o => { o.ContainerName = "uploads"; o.ConnectionString = cfg["Blobs:ConnectionString"]; });

// managed identity: the host supplies the credential from its own Azure.Identity reference
services.AddAzureBlobStorage(o =>
{
    o.ContainerName = "uploads";
    o.ServiceUri = new Uri("https://acme.blob.core.windows.net");
    o.Credential = new DefaultAzureCredential();
});
```

- `KeyPrefix` places the app's blobs under one folder of a shared container.
- `CreateContainerIfMissing` creates the container on the first write; leave it off where infrastructure owns containers.
- Listings carry the content type, unlike S3 listings.
