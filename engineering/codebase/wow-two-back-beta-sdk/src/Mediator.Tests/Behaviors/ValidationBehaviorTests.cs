using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Validation;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="ValidatingInterceptor{TRequest,TResponse}"/> — runs every registered <see cref="IValidator{T}"/>;
/// passes through to the handler when all are valid, throws (short-circuiting the handler) when any fails.
/// </summary>
public sealed class ValidationBehaviorTests
{
    private sealed record Req(int Age) : IRequest<string>;

    // Test-double validator: throws ValidationException from ValidateAndThrow when the predicate fails.
    private sealed class PredicateValidator(Func<Req, bool> isValid, string code = "rule") : IValidator<Req>
    {
        public ValidationError? Validate(Req instance) => isValid(instance)
            ? null
            : ValidationError.From([new FieldError { Property = nameof(Req.Age), Message = "invalid", Code = code }]);

        public void ValidateAndThrow(Req instance)
        {
            var error = Validate(instance);
            if (error is not null)
                throw new ValidationException(error);
        }

        public IReadOnlyList<FieldError> Inspect(Req instance) => Validate(instance)?.Failures ?? [];
    }

    [Fact]
    public async Task HandleAsync_ShouldPassThroughToHandler_WhenAllValidatorsPass()
    {
        var handlerRan = false;
        var behavior = new ValidatingInterceptor<Req, string>([new PredicateValidator(r => r.Age >= 0)]);

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
        var behavior = new ValidatingInterceptor<Req, string>([new PredicateValidator(r => r.Age >= 0)]);

        var act = async () => await behavior.HandleAsync(
            new Req(-1),
            () => { handlerRan = true; return ValueTask.FromResult("ok"); },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        handlerRan.Should().BeFalse("a failed validator must short-circuit the pipeline before the handler");
    }

    [Fact]
    public async Task HandleAsync_ShouldAggregateFailures_FromEveryValidator()
    {
        var calls = new List<string>();
        var behavior = new ValidatingInterceptor<Req, string>(
        [
            new PredicateValidator(_ =>
            {
                calls.Add("first");
                return false;
            }, "first.rule"),
            new PredicateValidator(_ =>
            {
                calls.Add("second");
                return false;
            }, "second.rule"),
        ]);

        var act = async () => await behavior.HandleAsync(new Req(1), () => ValueTask.FromResult("ok"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        calls.Should().Equal("first", "second");
        exception.Which.ValidationError.Failures.Select(failure => failure.Code)
            .Should().Equal("first.rule", "second.rule");
    }

    [Fact]
    public async Task HandleAsync_ShouldPassThrough_WhenNoValidatorsRegistered()
    {
        var behavior = new ValidatingInterceptor<Req, string>([]);

        var result = await behavior.HandleAsync(new Req(-99), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok"); // nothing to validate → handler runs
    }

    // Test-double validator reporting fixed failures at their declared severities.
    private sealed class FixedValidator(params FieldError[] failures) : IValidator<Req>
    {
        public ValidationError? Validate(Req instance)
        {
            var errors = failures.Where(failure => failure.Severity == ValidationSeverity.Error).ToArray();
            return errors.Length == 0 ? null : ValidationError.From(errors);
        }

        public void ValidateAndThrow(Req instance)
        {
            var error = Validate(instance);
            if (error is not null)
                throw new ValidationException(error);
        }

        public IReadOnlyList<FieldError> Inspect(Req instance) => failures;
    }

    private static FieldError Finding(string code, ValidationSeverity severity) =>
        new() { Property = nameof(Req.Age), Message = code, Code = code, Severity = severity };

    [Fact]
    public async Task HandleAsync_ShouldRunHandlerAndTrackAdvisories_WhenOnlyWarningsFire()
    {
        var tracker = new ValidationAdvisoryTracker();
        var behavior = new ValidatingInterceptor<Req, string>(
            [new FixedValidator(Finding("warn", ValidationSeverity.Warning), Finding("hint", ValidationSeverity.Info))],
            tracker);

        var result = await behavior.HandleAsync(new Req(1), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
        tracker.Advisories.Select(advisory => advisory.Code).Should().Equal("warn", "hint");
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowOnlyErrorsAndTrackNothing_WhenAnErrorFires()
    {
        var tracker = new ValidationAdvisoryTracker();
        var behavior = new ValidatingInterceptor<Req, string>(
            [new FixedValidator(Finding("warn", ValidationSeverity.Warning), Finding("block", ValidationSeverity.Error))],
            tracker);

        var act = async () => await behavior.HandleAsync(new Req(1), () => ValueTask.FromResult("ok"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ValidationError.Failures.Select(failure => failure.Code).Should().Equal("block");
        tracker.Advisories.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ShouldPassWarningsThrough_WhenNoTrackerIsRegistered()
    {
        var behavior = new ValidatingInterceptor<Req, string>(
            [new FixedValidator(Finding("warn", ValidationSeverity.Warning))]);

        var result = await behavior.HandleAsync(new Req(1), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }
}
