using AwesomeAssertions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>
/// P0-10 — the Dapper conventions are process-global and latched once, so they are asserted rather than assumed.
/// </summary>
/// <remarks>
/// - <c>AddDapperConventions</c> latches its body with <c>Interlocked</c>; the second call is a silent no-op
/// - <c>DefaultTypeMap.MatchNamesWithUnderscores</c> is process-global, so test order changes a later test's SQL
/// - the suite pins parallelism off (see <c>AssemblyInfo.cs</c>) and this case pins the value
/// </remarks>
public sealed class DapperConventionLatchTests
{
    [Fact]
    public void AddDapperConventions_applies_underscore_matching_and_latches_after_the_first_call()
    {
        new ServiceCollection().AddDapperConventions();
        DefaultTypeMap.MatchNamesWithUnderscores.Should().BeTrue(); // snake_case column → PascalCase property mapping is on

        var original = DefaultTypeMap.MatchNamesWithUnderscores;
        try
        {
            // Anything that flips the global afterwards wins permanently: the latch makes re-registration a silent no-op,
            // which is exactly how a second configuration (or another suite's harness) changes this suite's SQL.
            DefaultTypeMap.MatchNamesWithUnderscores = false;
            new ServiceCollection().AddDapperConventions();

            DefaultTypeMap.MatchNamesWithUnderscores.Should().BeFalse(); // latched — the second call did NOT restore the convention
        }
        finally
        {
            DefaultTypeMap.MatchNamesWithUnderscores = original;
        }
    }

    [Fact]
    public void SqlNamingOptions_defaults_are_snake_columns_and_camel_parameters()
    {
        var naming = new SqlNamingOptions();

        naming.ColumnCase.Should().Be(CaseStyle.Snake);     // every composed fragment names snake_case columns
        naming.ParameterCase.Should().Be(CaseStyle.Camel);  // …and camelCase parameters
        SqlNamingMapper.Col("OrderLineId", naming.ColumnCase).Should().Be("order_line_id");
        SqlNamingMapper.ParRef("OrderLineId", naming.ParameterCase).Should().Be("@orderLineId");
    }

    [Fact]
    public void SqlNamingMapper_honours_the_style_it_is_handed_rather_than_any_default()
    {
        // The casing now travels as an argument, so two callers can disagree inside one process.
        SqlNamingMapper.Col("OrderLineId", CaseStyle.Camel).Should().Be("orderLineId");
        SqlNamingMapper.ParRef("OrderLineId", CaseStyle.Snake).Should().Be("@order_line_id");
    }

    [Fact]
    public void AddDapperConventions_registers_the_casing_a_caller_configured()
    {
        var provider = new ServiceCollection()
            .AddDapperConventions(naming => naming.ColumnCase = CaseStyle.Pascal)
            .BuildServiceProvider();

        provider.GetRequiredService<SqlNamingOptions>().ColumnCase.Should().Be(CaseStyle.Pascal);
    }
}
