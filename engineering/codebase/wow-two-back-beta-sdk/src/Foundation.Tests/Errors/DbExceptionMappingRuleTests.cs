using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Errors;

/// <summary>Covers the SDK's data-layer <see cref="IExceptionMappingRule"/> (lives under Data, exercised here alongside the rest of the errors layer).</summary>
public sealed class DbExceptionMappingRuleTests
{
    private static readonly DbExceptionMappingRule Rule = new();

    private static PostgresException Postgres(string sqlState)
        => new("boom", "ERROR", "ERROR", sqlState);

    [Fact]
    public void TryMap_ShouldReturnConflict_WhenUniqueViolation()
        => Rule.TryMap(Postgres("23505"))!.Type.Should().Be(AppErrorType.Conflict);

    [Fact]
    public void TryMap_ShouldReturnConflict_WhenDbUpdateConcurrency()
        => Rule.TryMap(new DbUpdateConcurrencyException())!.Type.Should().Be(AppErrorType.Conflict);

    [Theory]
    [InlineData("57P03")]
    [InlineData("53300")]
    public void TryMap_ShouldReturnExternalUnavailable_WhenServerUnavailable(string sqlState)
        => Rule.TryMap(Postgres(sqlState))!.Type.Should().Be(AppErrorType.ExternalUnavailable);

    [Fact]
    public void TryMap_ShouldReturnDbTimeout_WhenTimeout()
        => Rule.TryMap(new TimeoutException())!.Type.Should().Be(AppErrorType.DbTimeout);

    [Fact]
    public void TryMap_ShouldReturnNull_WhenNotADatabaseException()
        => Rule.TryMap(new InvalidOperationException("x")).Should().BeNull();

    [Fact]
    public void From_ShouldFallBackToUnexpected_WhenNotADatabaseException()
        => DbErrors.From(new InvalidOperationException("x")).Type.Should().Be(AppErrorType.Unexpected);
}
