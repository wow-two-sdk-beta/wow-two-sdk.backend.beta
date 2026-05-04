# WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks

> `Microsoft.Extensions.Diagnostics.HealthChecks` + Xabaril provider checks pre-installed (SqlServer, Postgres, MySql, Redis, RabbitMQ, Kafka, Mongo, Elasticsearch, Network, Uris, AzureServiceBus, AzureStorage, AwsS3, AwsSqs).

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks
```

## Usage

```csharp
builder.Services
    .AddWowTwoHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Db")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!)
    .AddRabbitMQ(builder.Configuration.GetConnectionString("Mq")!)
    .AddCheck("self", () => HealthCheckResult.Healthy());

var app = builder.Build();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
```

## See also

- [Xabaril AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
