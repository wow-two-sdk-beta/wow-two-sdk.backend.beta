# Storage

*Provider-neutral blob (file object) storage — one `IBlobRepository` surface over the local filesystem or an S3-compatible bucket; Azure Blob / GCS later.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Storage`. The core + local impl are pure BCL; the S3 adapter uses `AWSSDK.S3`.

## Surface

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `IBlobRepository`, `BlobInfo`, `BlobStoragePathMapper` | Save/read/exists/delete/info/list over forward-slash paths; traversal-guarded |
| `FileSystem/` | `AddLocalBlobStorage(rootPath)`, `LocalFileBlobRepository` | Filesystem-backed store (dev / single-node) |
| `S3/` | `AddS3BlobStorage(o => …)`, `S3BlobRepository` | S3-compatible bucket (AWS, R2, MinIO, …) via `AWSSDK.S3` |

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

## Notes

- Paths are logical, forward-slash, relative to the store root; `BlobStoragePathMapper` rejects `..`/absolute segments so a path can never escape the root.
- `OpenReadAsync` returns `null` (not throw) when a blob is absent; the caller disposes the returned stream.
- The local store does not persist content types (`BlobInfo.ContentType` is null); cloud adapters will.

## Roadmap (not yet built)

Cloud adapters on the same `IBlobRepository` surface — `Storage.S3` (AWSSDK.S3) · `Storage.Azure` (Azure.Storage.Blobs) · `Storage.Gcs` · a `FluentStorage` multi-cloud adapter · presigned-URL helpers · `ImageSharp` processing (Media).
