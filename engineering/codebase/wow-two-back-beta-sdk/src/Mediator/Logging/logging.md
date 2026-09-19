# WoW.Two.Sdk.Backend.Beta.Mediator.Logging

> Request interceptor — opens a `WoW.Two.Mediator` span and logs request start + successful completion through source-generated log methods.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Mediator.Logging
```

## Usage

```csharp
builder.Services.AddMediator(typeof(Program).Assembly);
builder.Services.AddMediatorLoggingBehavior();
```

Success output:
```
→ GetUser
← GetUser in 4ms
```

A thrown failure propagates without another error log. The exception/result handling boundary records it once.
