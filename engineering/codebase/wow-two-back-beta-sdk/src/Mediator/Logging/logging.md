# WoW.Two.Sdk.Backend.Beta.Mediator.Logging

> Pipeline behavior — logs request name + elapsed time using source-generated log methods (`[LoggerMessage]`).

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Mediator.Logging
```

## Usage

```csharp
builder.Services.AddMediator(typeof(Program).Assembly);
builder.Services.AddMediatorLoggingBehavior();
```

Output:
```
→ GetUser
← GetUser in 4ms
```

Failure:
```
✕ GetUser failed after 12ms — System.KeyNotFoundException
```
