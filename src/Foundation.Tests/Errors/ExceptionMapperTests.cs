using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Errors;

public sealed class ExceptionMapperTests
{
    /// <summary>A rule that maps one exception type to a fixed <see cref="AppErrorType"/>, deferring on anything else.</summary>
    private sealed class StubRule(Type match, AppErrorType type) : IExceptionMappingRule
    {
        public AppError? TryMap(Exception exception)
            => exception.GetType() == match ? AppError.Of(type, $"mapped {type}") : null;
    }

    [Fact]
    public void Map_ShouldUnwrapCarriedError_WhenAppException()
    {
        var mapper = new ExceptionMapper([]);
        var original = AppErrors.NotFound("missing");

        var error = mapper.Map(original.ToException());

        error.Should().BeSameAs(original);
    }

    [Fact]
    public void Map_ShouldFallBackToUnexpected_WhenNoRuleMatches()
    {
        var mapper = new ExceptionMapper([]);

        var error = mapper.Map(new InvalidOperationException("boom"));

        error.Type.Should().Be(AppErrorType.Unexpected);
    }

    [Fact]
    public void Map_ShouldUseMatchingRule_WhenRuleRecognizesException()
    {
        var mapper = new ExceptionMapper([new StubRule(typeof(FormatException), AppErrorType.Validation)]);

        var error = mapper.Map(new FormatException());

        error.Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public void Map_ShouldPreferLastRegisteredRule_WhenMultipleRulesMatch()
    {
        // Both rules match FormatException; the last-registered (an app rule) must shadow the earlier one.
        var mapper = new ExceptionMapper(
        [
            new StubRule(typeof(FormatException), AppErrorType.Validation),
            new StubRule(typeof(FormatException), AppErrorType.Conflict),
        ]);

        var error = mapper.Map(new FormatException());

        error.Type.Should().Be(AppErrorType.Conflict);
    }
}
