# Storage

*Provider-neutral blob (file object) storage — one `IBlobRepository` surface over the local filesystem, an S3-compatible bucket or Azure Blob Storage; GCS later.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Storage`. The core + local impl are pure BCL; the cloud adapters use `AWSSDK.S3` and `Azure.Storage.Blobs`.

## Surface

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `IBlobRepository`, `BlobInfo`, `BlobStoragePathMapper` | Save/read/exists/delete/info/list over forward-slash paths; traversal-guarded |
| `FileSystem/` | `AddLocalBlobStorage(rootPath)`, `LocalFileBlobRepository` | Filesystem-backed store (dev / single-node) |
| `S3/` | `AddS3BlobStorage(o => …)`, `S3BlobRepository` | S3-compatible bucket (AWS, R2, MinIO, …) via `AWSSDK.S3` |
| `Azure/` | `AddAzureBlobStorage(o => …)`, `AzureBlobRepository` | Azure Blob Storage container via `Azure.Storage.Blobs` |
| `Core/` | `IBlobUrlIssuer`, `BlobUrlResult` | Signed read/write URLs for direct uploads and downloads (S3 presign, Azure SAS) |
| `Transfer/` | `AddBlobTransferUrls(o => …)`, `MapBlobTransferEndpoints()` | HMAC-signed SDK routes that serve signed URLs over any repository, the local disk included |

## Quickstart

```csharp
builder.Services.AddLocalBlobStorage(Path.Combine(env.ContentRootPath, "blobs"));

public sealed class Avatars(IBlobRepository storage)
{
    public Task SaveAsync(string userId, Stream png, CancellationToken ct) =>
        storage.SaveAsync($"avatars/{userId}.png", png, "image/png", ct);

    public Task<Stream?> ReadAsync(string userId, CancellationToken ct) =>
        storage.OpenReadAsync($"avatars/{userId}.png", ct);

    public IAsyncEnumerable<BlobInfo> ListAsync(CancellationToken ct) =>
        storage.ListAsync("avatars/", ct);
}
```

## Direct uploads and downloads

```csharp
var upload = await urls.IssueWriteUrlAsync($"uploads/{id}.pdf", TimeSpan.FromMinutes(15), "application/pdf");
// the client PUTs to upload.Url with upload.Headers; the API never carries the bytes
var download = await urls.IssueReadUrlAsync($"uploads/{id}.pdf", TimeSpan.FromMinutes(5), "report.pdf");
```

- S3 presigns SigV4 locally (the registration forces SigV4, as AWS and R2 reject SigV2); Azure signs a SAS with
  the account key, or a user delegation key under a token credential.
- The local store, or any store without native signing, uses `AddBlobTransferUrls` + `MapBlobTransferEndpoints()`:
  URLs signed with HMAC over method, path, expiry and file name or media type; 403 when altered or expired.
- URLs live up to 7 days; an upload URL issued with a media type only accepts that type.

## Notes

- Paths are logical, forward-slash, relative to the store root; `BlobStoragePathMapper` rejects `..`/absolute segments so a path can never escape the root.
- `OpenReadAsync` returns `null` (not throw) when a blob is absent; the caller disposes the returned stream.
- The local store does not persist content types (`BlobInfo.ContentType` is null); cloud adapters will.

## Roadmap (not yet built)

Cloud adapters on the same `IBlobRepository` surface — `Storage.S3` (AWSSDK.S3) · `Storage.Azure` (Azure.Storage.Blobs) · `Storage.Gcs` · a `FluentStorage` multi-cloud adapter · presigned-URL helpers · `ImageSharp` processing (Media).
