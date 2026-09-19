using System.Globalization;
using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Results;

/// <summary>Covers <see cref="Result{TSuccess,TFailure}"/> — the carrier whose failure arm a caller switches on.</summary>
public sealed class TypedFailureResultTests
{
    private enum OrderFailure
    {
        OutOfStock,
        PaymentDeclined,
    }

    [Fact]
    public void Ok_ShouldBuildSuccess()
    {
        var result = Result<int, OrderFailure>.Ok(42);

        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Result<int, OrderFailure>.Success>()
            .Which.Value.Should().Be(42);
    }

    [Fact]
    public void Fail_ShouldBuildFailureCarryingTheCase()
    {
        var result = Result<int, OrderFailure>.Fail(OrderFailure.OutOfStock);

        result.IsSuccess.Should().BeFalse();
        result.Should().BeOfType<Result<int, OrderFailure>.Failure>()
            .Which.Error.Should().Be(OrderFailure.OutOfStock);
    }

    [Fact]
    public void Match_ShouldBranchOnTheCase()
    {
        static string Describe(Result<int, OrderFailure> result) => result.Match(
            value => $"ok:{value}",
            failure => failure switch
            {
                OrderFailure.OutOfStock => "restock",
                OrderFailure.PaymentDeclined => "retry",
                _ => "unknown",
            });

        Describe(Result<int, OrderFailure>.Ok(7)).Should().Be("ok:7");
        Describe(Result<int, OrderFailure>.Fail(OrderFailure.PaymentDeclined)).Should().Be("retry");
    }

    [Fact]
    public void Map_ShouldTransformSuccessAndPropagateFailure()
    {
        Result<int, OrderFailure>.Ok(3).Map(value => $"n={value}")
            .Should().BeOfType<Result<string, OrderFailure>.Success>()
            .Which.Value.Should().Be("n=3");

        Result<int, OrderFailure>.Fail(OrderFailure.OutOfStock).Map(value => value.ToString(CultureInfo.InvariantCulture))
            .Should().BeOfType<Result<string, OrderFailure>.Failure>()
            .Which.Error.Should().Be(OrderFailure.OutOfStock);
    }

    [Fact]
    public void ToResult_ShouldTranslateTheCaseIntoAnAppError()
    {
        Result<int, OrderFailure>.Fail(OrderFailure.PaymentDeclined)
            .ToResult(failure => AppErrorFactory.Conflict(failure.ToString()))
            .Should().BeOfType<Result<int>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Conflict);

        Result<int, OrderFailure>.Ok(1).ToResult(_ => AppErrorFactory.Unexpected())
            .Should().BeOfType<Result<int>.Success>()
            .Which.Value.Should().Be(1);
    }

    [Fact]
    public void Match_ShouldThrow_WhenDelegatesNull()
    {
        var act = () => Result<int, OrderFailure>.Ok(1).Match(null!, _ => 0);

        act.Should().Throw<ArgumentNullException>();
    }
}
