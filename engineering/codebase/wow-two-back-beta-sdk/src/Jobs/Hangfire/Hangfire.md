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
- Storage chosen at startup: `AddHangfireJobs((provider, cfg) => …, opts)` reads the built provider, so a test host's
  settings reach it — e.g. pick `cfg.UseInMemoryStorage()` or `cfg.UsePostgresPersistenceStorage(provider)` from a setting.
- On the persistence floor's database: `AddPostgresHangfireJobs(opts, storage => storage.QueuePollInterval = …)` resolves
  `DatabaseSettings` from `AddPostgresPersistence` — one connection string, `DB_CONNECTION` override included.
- Short retry delays: set `opts.SchedulePollingInterval` — Hangfire moves delayed jobs every 15 s by default.
- Dashboard off-loopback: pass `localRequestsOnly: false` **only** behind your own auth.
- ⚠️ License: Hangfire is **LGPL-3.0** — the sole exception to the permissive-only rule, blessed by `targets.md` §6 (P4). Revisit before any non-beta distill.
