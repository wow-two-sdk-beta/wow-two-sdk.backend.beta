# WoW.Two.Sdk.Backend.Beta.Testing.WireMock

> [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) async fixture (`WireMockFixture`).

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.WireMock
```

## Usage

```csharp
private readonly WireMockFixture _http = new();

public override async ValueTask InitializeAsync()
{
    await _http.StartAsync();
    _http.Server.Given(Request.Create().WithPath("/users/42").UsingGet())
                .RespondWith(Response.Create().WithBodyAsJson(new { id = 42, name = "Alice" }));
}

protected override WebApiTestHost<Program> BuildHost() => new()
{
    ConfigureServicesHook = s =>
        s.AddHttpClient("UpstreamApi", c => c.BaseAddress = new Uri(_http.Url))
};
```

## See also

- [WireMock.Net docs](https://github.com/WireMock-Net/WireMock.Net/wiki)
