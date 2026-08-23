using AwesomeAssertions;
using FluentValidation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using Xunit;
using ValidationException = WoW.Two.Sdk.Backend.Beta.Foundation.Validation.ValidationException;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Validation;

public sealed class FluentValidationAdapterTests
{
    private sealed record Person(string Name, int Age);

    private sealed class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Age).GreaterThanOrEqualTo(0);
        }
    }

    private sealed class BoundedPersonValidator : AbstractValidator<Person>
    {
        public BoundedPersonValidator()
        {
            RuleFor(p => p.Name).MaximumLength(3);
            RuleFor(p => p.Age).InclusiveBetween(13, 120);
        }
    }

    private sealed record Credentials(string Password);

    /// <summary>Sign-up: the user needs to be told the required length, so the operand is published.</summary>
    private sealed class SignUpValidator : AbstractValidator<Credentials>
    {
        public SignUpValidator() => RuleFor(c => c.Password).MinimumLength(12);
    }

    /// <summary>Sign-in: the same member, the same rule, but nothing about the secret may leave.</summary>
    private sealed class SignInValidator : AbstractValidator<Credentials>, ISensitiveMembers
    {
        public IReadOnlySet<string> SensitiveMembers { get; } =
            new HashSet<string>(StringComparer.Ordinal) { nameof(Credentials.Password) };

        public SignInValidator() => RuleFor(c => c.Password).MinimumLength(12);
    }

    private sealed class AdvisoryPersonValidator : AbstractValidator<Person>
    {
        public AdvisoryPersonValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Age).GreaterThanOrEqualTo(18).WithSeverity(Severity.Warning);
        }
    }

    private static FluentValidationAdapter<Person> Adapter()
        => new([new PersonValidator()]);

    [Fact]
    public void Validate_ShouldReturnNull_WhenValid()
    {
        var error = Adapter().Validate(new Person("Ada", 30));

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldReturnValidationErrorWithFailures_WhenInvalid()
    {
        var error = Adapter().Validate(new Person("", -1));

        error.Should().NotBeNull();
        error!.Type.Should().Be(AppErrorType.Validation);
        error.Failures.Should().HaveCount(2);
        error.Failures.Should().Contain(f => f.Property == nameof(Person.Name));
        error.Failures.Should().Contain(f => f.Property == nameof(Person.Age));
    }

    [Fact]
    public void Validate_ShouldReturnNull_WhenNoValidatorsRegistered()
    {
        var adapter = new FluentValidationAdapter<Person>([]);

        adapter.Validate(new Person("", -1)).Should().BeNull();
    }

    [Fact]
    public void ValidateAndThrow_ShouldDoNothing_WhenValid()
    {
        var adapter = Adapter();

        var act = () => adapter.ValidateAndThrow(new Person("Grace", 25));

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_ShouldThrowValidationException_WhenInvalid()
    {
        var adapter = Adapter();

        var act = () => adapter.ValidateAndThrow(new Person("", -1));

        act.Should().Throw<ValidationException>()
            .Which.ValidationError.Failures.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_ShouldCarryRuleOperands_AsParams()
    {
        var adapter = new FluentValidationAdapter<Person>([new BoundedPersonValidator()]);

        var error = adapter.Validate(new Person("abcd", 5));

        error.Should().NotBeNull();
        var name = error!.Failures.Single(f => f.Property == nameof(Person.Name));
        name.Params.Should().NotBeNull();
        name.Params!["MaxLength"].Should().Be(3);

        var age = error.Failures.Single(f => f.Property == nameof(Person.Age));
        age.Params!["From"].Should().Be(13);
        age.Params["To"].Should().Be(120);
    }

    [Fact]
    public void Validate_ShouldNotLeakRejectedValue_IntoParams()
    {
        // FluentValidation puts the REJECTED VALUE in every failure's placeholder dictionary. Copying it
        // verbatim would echo a rejected password or token into the HTTP response, and from there into logs.
        var adapter = new FluentValidationAdapter<Person>([new BoundedPersonValidator()]);

        var error = adapter.Validate(new Person("hunter2-secret", 5));

        var name = error!.Failures.Single(f => f.Property == nameof(Person.Name));
        name.Params.Should().NotContainKey("PropertyValue");
        name.Params.Should().NotContainKey("PropertyPath");
        name.Params!.Values.Should().NotContain("hunter2-secret");
    }

    [Fact]
    public void Validate_ShouldPublishOperands_WhenValidatorDeclaresNoSensitiveMembers()
    {
        // Sign-up: the user cannot satisfy a length rule nobody told them about.
        var adapter = new FluentValidationAdapter<Credentials>([new SignUpValidator()]);

        var error = adapter.Validate(new Credentials("short"));

        error!.Failures.Single().Params!["MinLength"].Should().Be(12);
    }

    [Fact]
    public void Validate_ShouldDropAllParams_WhenValidatorDeclaresMemberSensitive()
    {
        // Sign-in: same member, same rule, opposite call. `TotalLength` alone discloses the secret's length,
        // so the whole operand set goes — sensitivity belongs to the operation, not to the member's name.
        var adapter = new FluentValidationAdapter<Credentials>([new SignInValidator()]);

        var error = adapter.Validate(new Credentials("short"));

        var failure = error!.Failures.Single();
        failure.Params.Should().BeNull();
        failure.Code.Should().Be("MinimumLengthValidator");
        failure.Message.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_ShouldIgnoreAdvisoryFailures()
    {
        var adapter = new FluentValidationAdapter<Person>([new AdvisoryPersonValidator()]);

        var error = adapter.Validate(new Person("Ada", 15));

        error.Should().BeNull();
    }

    [Fact]
    public void Inspect_ShouldReportEveryFailure_AtEverySeverity()
    {
        var adapter = new FluentValidationAdapter<Person>([new AdvisoryPersonValidator()]);

        var failures = adapter.Inspect(new Person("", 15));

        failures.Should().HaveCount(2);
        failures.Single(f => f.Property == nameof(Person.Name)).Severity.Should().Be(ValidationSeverity.Error);
        failures.Single(f => f.Property == nameof(Person.Age)).Severity.Should().Be(ValidationSeverity.Warning);
    }

    [Fact]
    public void Inspect_ShouldReportSameCodesAndParams_AsValidate()
    {
        // The advisory read is the same rules — only the verdict differs, and `Inspect` makes none.
        var adapter = new FluentValidationAdapter<Person>([new BoundedPersonValidator()]);
        var instance = new Person("abcd", 5);

        var inspected = adapter.Inspect(instance).Single(f => f.Property == nameof(Person.Name));
        var validated = adapter.Validate(instance)!.Failures.Single(f => f.Property == nameof(Person.Name));

        inspected.Code.Should().Be(validated.Code);
        inspected.Params!["MaxLength"].Should().Be(validated.Params!["MaxLength"]);
    }

    [Fact]
    public void Inspect_ShouldReturnEmpty_WhenNothingFires()
    {
        var adapter = new FluentValidationAdapter<Person>([new AdvisoryPersonValidator()]);

        adapter.Inspect(new Person("Ada", 30)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReportOnlyDisplayName_WhenRuleHasNoOperands()
    {
        var error = Adapter().Validate(new Person("", 30));

        var name = error!.Failures.Single(f => f.Property == nameof(Person.Name));
        // A presence rule compares against nothing, so it contributes no operands. `PropertyName` survives
        // because it is the DISPLAY name — `.WithName()` / a `DisplayNameResolver` makes it diverge from
        // `Property`, which stays the member path.
        name.Params.Should().ContainSingle().Which.Key.Should().Be("PropertyName");
    }

    [Fact]
    public void ValidationException_ShouldBeAppExceptionCarryingValidationError()
    {
        var error = ValidationError.From([new FieldError { Property = "x", Message = "bad", Code = "rule" }]);

        var exception = new ValidationException(error);

        exception.Should().BeAssignableTo<AppException>();
        exception.Error.Should().BeSameAs(error);
        exception.ValidationError.Should().BeSameAs(error);
    }
}
