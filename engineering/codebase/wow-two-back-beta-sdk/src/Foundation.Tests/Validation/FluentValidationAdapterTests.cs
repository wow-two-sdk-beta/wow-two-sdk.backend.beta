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
    public void ValidationException_ShouldBeAppExceptionCarryingValidationError()
    {
        var error = ValidationError.From([new FieldError { Property = "x", Message = "bad", Code = "rule" }]);

        var exception = new ValidationException(error);

        exception.Should().BeAssignableTo<AppException>();
        exception.Error.Should().BeSameAs(error);
        exception.ValidationError.Should().BeSameAs(error);
    }
}
