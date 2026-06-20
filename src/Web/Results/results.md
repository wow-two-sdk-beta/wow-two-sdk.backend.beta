# Web.Results

Maps application failure results to HTTP.

- **`FailureCategoryExtensions.ToStatusCode(this FailureCategory)`** — the conventional category→status map (400 · 401 · 402 · 403 · 404 · 409 · 500). Pairs with `Mediator.Result.ICategorizedFailure` (the `{ ErrorMessage, Category }` failure shape): a controller resolves the status from `failure.Category.ToStatusCode()`, keeping the application layer HTTP-free.

See also: `Mediator.Result` (`FailureCategory`, `ICategorizedFailure`), `Web.Contracts` (the success envelope).
