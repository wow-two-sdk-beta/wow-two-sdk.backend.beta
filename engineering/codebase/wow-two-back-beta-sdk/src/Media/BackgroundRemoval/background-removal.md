# Background removal

Optional, bounded HTTP adapter for host-owned segmentation workers. Configure URL/key explicitly; absent configuration reports unavailable. No model downloads occur.

```csharp
services.AddBackgroundRemoval(o => {
    o.BaseUrl = "http://127.0.0.1:8290";
    o.ApiKey = configuration["PrivateWorkerKey"];
});
```

See [contract](BackgroundRemoval.standard.md) and [API](BackgroundRemoval.spec.md).
