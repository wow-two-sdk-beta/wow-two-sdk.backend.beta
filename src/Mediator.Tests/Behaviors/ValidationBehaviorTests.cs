using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Validation;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="ValidationBehavior{TRequest,TResponse}"/> — runs every registered <see cref="IValidator{T}"/>;
/// passes through to the handler when all are valid, throws (short-circuiting the handler) when any fails.
/// </summary>
public sealed class ValidationBehaviorTests
{
    private sealed record Req(int Age) : IRequest<string>;

    // Test-double validator: throws ValidationException from ValidateAndThrow when the predicate fails.
    private sealed class PredicateValidator(Func<Req, bool> isValid) : IValidator<Req>
    {
        public ValidationResult Validate(Req instance) => isValid(instance)
            ? new ValidationResult.Success()
            : new ValidationResult.Failure([new ValidationError(nameof(Req.Age), "invalid", "rule")]);

        public void ValidateAndThrow(Req instance)
        {
            if (!isValid(instance))
                throw new ValidationException([new ValidationError(nameof(Req.Age), "invalid", "rule")]);
        }
    }

    [Fact]
    public async Task Passes_through_to_handler_when_all_validators_pass()
    {
        var handlerRan = false;
        var behavior = new ValidationBehavior<Req, string>([new PredicateValidator(r => r.Age >= 0)]);

        var result = await behavior.HandleAsync(
            new Req(18),
            () => { handlerRan = true; return ValueTask.FromResult("ok"); },
            CancellationToken.None);

        handlerRan.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Throws_and_skips_handler_when_a_validator_fails()
    {
        var handlerRan = false;
        var behavior = new ValidationBehavior<Req, string>([new PredicateValidator(r => r.Age >= 0)]);

        var act = async () => await behavior.HandleAsync(
            new Req(-1),
            () => { handlerRan = true; return ValueTask.FromResult("ok"); },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        handlerRan.Should().BeFalse("a failed validator must short-circuit the pipeline before the handler");
    }

    [Fact]
    public async Task Runs_all_validators_failing_on_any()
    {
        // First passes, second fails → still throws.
        var behavior = new ValidationBehavior<Req, string>(
        [
            new PredicateValidator(_ => true),
            new PredicateValidator(_ => false),
        ]);

        var act = async () => await behavior.HandleAsync(new Req(1), () => ValueTask.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task No_validators_registered_passes_through()
    {
        var behavior = new ValidationBehavior<Req, string>([]);

        var result = await behavior.HandleAsync(new Req(-99), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok"); // nothing to validate → handler runs
    }
}
