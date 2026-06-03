# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitHub

> GitHub OAuth provider via `AspNet.Security.OAuth.GitHub`.

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
        "user:email", "read:org");
```
