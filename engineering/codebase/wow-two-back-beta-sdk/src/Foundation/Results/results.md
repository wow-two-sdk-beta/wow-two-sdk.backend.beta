# Foundation.Results

`Result`, `Result<T>` and `Result<TSuccess, TFailure>` — outcomes that carry an `AppError` instead of throwing.

## Chaining

```csharp
var receipt = await LoadOrder(id)                                  // Result<Order>
    .Ensure(order => order.IsOpen, order => AppErrorFactory.Conflict($"Order {order.Id} is closed."))
    .Tap(order => logger.LogInformation("Paying {Id}", order.Id))
    .BindAsync(order => payments.ChargeAsync(order))             // Task<Result<Payment>>
    .Map(payment => new ReceiptDto(payment.Id));                  // Task<Result<ReceiptDto>>
```

| Combinator | Does |
|---|---|
| `Map` / `Bind` / `BindAsync` | continue on success; `Task<Result<T>>` overloads keep chains flat |
| `Tap` / `TapAsync` / `TapError` | side effect on one case, result unchanged |
| `Ensure` | a failed check becomes the given failure |
| `ValueOr` / `ValueOrThrow` | unwrap with a fallback, or throw the failure |
| `Combine` | all succeeded → values; else the single failure or an `AppAggregateError` |
| `ToResult` | drop the value |

- Every combinator passes an existing failure through unchanged and never catches exceptions; use `Attempt` for that.
