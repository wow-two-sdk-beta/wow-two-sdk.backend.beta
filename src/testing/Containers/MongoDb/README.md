# WoW.Two.Sdk.Backend.Beta.Testing.Containers.MongoDb

> MongoDB Testcontainers fixture.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Containers.MongoDb
```

## Usage

```csharp
private readonly MongoDbFixture _mongo = new();

// Pass _mongo.ConnectionString to MongoDB.Driver
```
