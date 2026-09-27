# WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks

> `Microsoft.Extensions.Diagnostics.HealthChecks` + Xabaril provider checks pre-installed (SqlServer, Postgres, MySql, Redis, RabbitMQ, Kafka, Mongo, Elasticsearch, Network, Uris, AzureServiceBus, AzureStorage, AwsS3, AwsSqs).

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks
```

## Usage

```csharp
builder.Services
    .AddHealthChecksBuilder()
    .AddNpgSql(builder.Configuration.GetConnectionString("Db")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!)
    .AddRabbitMQ(builder.Configuration.GetConnectionString("Mq")!)
    .AddCheck("self", () => HealthCheckResult.Healthy());

var app = builder.Build();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
```

## Database readiness

```csharp
builder.Services.AddHealthChecks().AddDatabaseReadinessCheck<AppDbContext>();   // name "database", tag "ready"
```

The check resolves a context per probe and reports `Unhealthy` while `CanConnectAsync` fails, so a rollout
waits for the database instead of routing traffic to a host that cannot serve it.

## See also

- [Xabaril AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
