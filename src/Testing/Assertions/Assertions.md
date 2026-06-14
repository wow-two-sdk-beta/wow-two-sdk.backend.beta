# WoW.Two.Sdk.Backend.Beta.Testing.Assertions

> Bundles [AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions) (the OSS fork of FluentAssertions, post v8 commercial license) and [Shouldly](https://github.com/shouldly/shouldly).

## Why

FluentAssertions changed its license to Xceed in 2025 (commercial for v8+). AwesomeAssertions is the community fork preserving the old API under Apache-2.0. We bundle both — pick whichever matches your team's style.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Assertions
```

## Usage

### AwesomeAssertions (FluentAssertions API)

```csharp
using AwesomeAssertions;

result.Should().Be(42);
list.Should().HaveCount(3).And.OnlyContain(x => x.IsValid);
```

### Shouldly

```csharp
using Shouldly;

result.ShouldBe(42);
list.Count.ShouldBe(3);
list.ShouldAllBe(x => x.IsValid);
```

## See also

- [AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions)
- [Shouldly](https://github.com/shouldly/shouldly)
