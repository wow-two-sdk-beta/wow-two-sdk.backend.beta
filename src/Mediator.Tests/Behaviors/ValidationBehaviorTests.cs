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
        public ValidationError? Validate(Req instance) => isValid(instance)
            ? null
            : ValidationError.From([new FieldError { Property = nameof(Req.Age), Message = "invalid", Code = "rule" }]);

        public void ValidateAndThrow(Req instance)
        {
            var error = Validate(instance);
            if (error is not null)
                throw new ValidationException(error);
        }
    }

    [Fact]
    public async Task HandleAsync_ShouldPassThroughToHandler_WhenAllValidatorsPass()
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
    public async Task HandleAsync_ShouldThrowAndSkipHandler_WhenValidatorFails()
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
    public async Task HandleAsync_ShouldThrow_WhenAnyValidatorFails()
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
    public async Task HandleAsync_ShouldPassThrough_WhenNoValidatorsRegistered()
    {
        var behavior = new ValidationBehavior<Req, string>([]);

        var result = await behavior.HandleAsync(new Req(-99), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok"); // nothing to validate → handler runs
    }
}
