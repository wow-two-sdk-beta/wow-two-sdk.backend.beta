# SPA antiforgery

> Cross-site request forgery protection for single-page apps that authenticate with cookies.

```csharp
builder.Services.AddSpaAntiforgery(options => options.ExemptPathPrefixes.Add("/api/billing/webhook"));

app.UseApiDefaults(useIdentity: pipeline =>
{
    pipeline.UseAuthentication();
    pipeline.UseAuthorization();
    pipeline.UseSpaAntiforgery();   // after authentication: tokens bind to the signed-in user
});
```

```ts
// Client: echo the readable cookie on every unsafe request.
const token = document.cookie.match(/(?:^|; )XSRF-TOKEN=([^;]+)/)?.[1];
await fetch("/api/codes", { method: "POST", headers: { "X-XSRF-TOKEN": decodeURIComponent(token ?? "") }, body });
```

- Every safe request (`GET`, `HEAD`, `OPTIONS`, `TRACE`) refreshes the readable `XSRF-TOKEN` cookie.
- Unsafe requests that carry cookies must echo it in `X-XSRF-TOKEN`; otherwise they get a 400 problem.
- Requests without cookies pass: a browser cannot forge them, so bearer and server calls are unaffected.
- Exempt only callbacks that prove themselves another way, such as a signed payment webhook.
- Tokens rotate with sign-in; refresh the page state with a `GET` after authentication changes.
- Tests: `host.CreateSpaClient("/health")` keeps cookies and echoes tokens as the browser client does.
