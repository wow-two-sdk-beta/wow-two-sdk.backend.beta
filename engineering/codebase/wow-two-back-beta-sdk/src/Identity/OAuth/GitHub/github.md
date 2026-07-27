# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitHub

> GitHub OAuth provider via `AspNet.Security.OAuth.GitHub`. Scheme `GitHub`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitHub
```

## Usage

```csharp
builder.Services
    .AddAuthentication()
    .AddGitHubAuthentication(
        builder.Configuration["OAuth:GitHub:ClientId"]!,
        builder.Configuration["OAuth:GitHub:ClientSecret"]!,
        scopes: ["user:email", "read:org"]);
```

Optional `configure` delegate runs before the `wt:provider` stamp:

```csharp
.AddGitHubAuthentication(id, secret, o => o.Scope.Add("repo"), "user:email");
```

## Baseline

`SaveTokens=true` + scope merge + `wt:provider` stamp applied automatically — see [`../oauth.md`](../oauth.md).

## Quirk

- Request `user:email` to receive the verified primary email.
