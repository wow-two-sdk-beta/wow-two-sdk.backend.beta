# Jobs.Hangfire

Background jobs — fire-and-forget (`IBackgroundJobClient`), delayed, recurring
(`IRecurringJobManager`) — with SDK serializer conventions and a hosted processing server.

```csharp
// dev / single instance
builder.Services.AddInMemoryHangfireJobs();

// production
builder.Services.AddPostgresHangfireJobs(config.GetConnectionString("jobs")!,
    o => { o.WorkerCount = 8; o.Queues.Add("critical"); o.Queues.Add("default"); });

var app = builder.Build();
app.UseHangfireJobsDashboard();          // /jobs, local requests only by default

// usage
_jobs.Enqueue<IInvoiceService>(s => s.GenerateAsync(orderId));
_recurring.AddOrUpdate<ICleanupService>("nightly-cleanup", s => s.RunAsync(), Cron.Daily);
```

- Custom storage: `AddHangfireJobs(cfg => cfg.UseYourStorage(…), opts)` — SqlServer/Redis presets are future per the registry.
- Dashboard off-loopback: pass `localRequestsOnly: false` **only** behind your own auth.
- ⚠️ License: Hangfire is **LGPL-3.0** — the sole exception to the permissive-only rule, blessed by `targets.md` §6 (P4). Revisit before any non-beta distill.
