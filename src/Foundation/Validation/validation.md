# WoW.Two.Sdk.Backend.Beta.Validation

> FluentValidation registration helpers — assembly scanning by convention.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Validation
```

## Usage

```csharp
// Scan calling assembly for validators
builder.Services.AddFluentValidatorsFromAssemblies();

// Or specify assemblies explicitly
builder.Services.AddFluentValidatorsFromAssemblies(typeof(Program).Assembly);
```

Define validators normally:

```csharp
public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).InclusiveBetween(13, 120);
    }
}
```

## See also

- [FluentValidation docs](https://docs.fluentvalidation.net/)
