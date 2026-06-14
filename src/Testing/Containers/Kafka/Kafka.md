# WoW.Two.Sdk.Backend.Beta.Testing.Containers.Kafka

> Kafka Testcontainers fixture.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Containers.Kafka
```

## Usage

```csharp
private readonly KafkaFixture _kafka = new();

// Pass _kafka.BootstrapServers to your Kafka producer / consumer config
```
