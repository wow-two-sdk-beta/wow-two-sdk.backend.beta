# WoW.Two.Sdk.Backend.Beta.Testing.Bogus

> [Bogus](https://github.com/bchavez/Bogus) presets — deterministic seed, conventional `Faker<T>` factory.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Bogus
```

## Usage

```csharp
var users = BogusFakerFactory.For<User>()
    .RuleFor(u => u.Email, f => f.Internet.Email())
    .RuleFor(u => u.Name, f => f.Person.FullName)
    .Generate(10);
```

## See also

- [Bogus docs](https://github.com/bchavez/Bogus)
