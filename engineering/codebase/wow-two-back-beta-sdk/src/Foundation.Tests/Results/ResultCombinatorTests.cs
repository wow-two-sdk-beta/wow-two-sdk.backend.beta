using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Results;

/// <summary>Result combinators chain success and pass a failure through untouched.</summary>
public sealed class ResultCombinatorTests
{
    private static readonly AppError Missing = AppErrorFactory.NotFound("missing");

    [Fact]
    public void Bind_ContinuesOnSuccessAndShortCircuitsOnFailure()
    {
        var calls = 0;
        Result<int> Half(int value)
        {
            calls++;
            return value % 2 == 0 ? Result<int>.Ok(value / 2) : Result<int>.Fail(AppErrorFactory.Validation("odd"));
        }

        Result<int>.Ok(8).Bind(Half).Bind(Half).ValueOr(-1).Should().Be(2);
        Result<int>.Ok(6).Bind(Half).Bind(Half).Should().BeOfType<Result<int>.Failure>();
        Result<int>.Fail(Missing).Bind(Half).Match<AppError?>(_ => null, error => error).Should().Be(Missing);
        calls.Should().Be(4);
    }

    [Fact]
    public async Task AsyncChains_ComposeTasksOfResults()
    {
        static Task<Result<int>> LoadAsync(int id) => Task.FromResult(id > 0 ? Result<int>.Ok(id * 10) : Result<int>.Fail(Missing));

        var value = await Result<int>.Ok(4).BindAsync(LoadAsync).Map(total => total + 1).Bind(total => Result<string>.Ok($"#{total}"));
        value.ValueOr("none").Should().Be("#41");

        (await Result<int>.Ok(-1).BindAsync(LoadAsync).BindAsync(LoadAsync)).ValueOr(error => error.Message.Length).Should().Be(7);
    }

    [Fact]
    public async Task TapAndEnsure_RunOnlyOnTheMatchingCase()
    {
        var seen = new List<string>();

        var checkedResult = Result<int>.Ok(5)
            .Tap(value => seen.Add($"value {value}"))
            .Ensure(value => value > 10, value => AppErrorFactory.Validation($"{value} is too small"))
            .TapError(error => seen.Add(error.Message));
        await Result<int>.Fail(Missing).TapAsync(value => { seen.Add("never"); return Task.CompletedTask; });

        checkedResult.Should().BeOfType<Result<int>.Failure>();
        seen.Should().Equal("value 5", "5 is too small");
        Result<int>.Ok(20).Ensure(value => value > 10, Missing).ValueOr(0).Should().Be(20);
    }

    [Fact]
    public void Combine_CollectsValuesOrAggregatesEveryFailure()
    {
        new[] { Result<int>.Ok(1), Result<int>.Ok(2) }.Combine().ValueOr([]).Should().Equal(1, 2);

        var failed = new[] { Result<int>.Ok(1), Result<int>.Fail(Missing), Result<int>.Fail(AppErrorFactory.Conflict("taken")) }.Combine();
        failed.Match<AppError?>(_ => null, error => error).Should().BeOfType<AppAggregateError>()
            .Which.Errors.Select(error => error.Message).Should().Equal("missing", "taken");

        new[] { Result.Ok(), Result.Fail(Missing) }.Combine().Should().BeOfType<Result.Failure>().Which.Error.Should().Be(Missing);
        Result.Ok().Bind(() => Result<string>.Ok("next")).ValueOr("none").Should().Be("next");
        Result<int>.Ok(3).ToResult().Should().BeOfType<Result.Success>();
    }
}
