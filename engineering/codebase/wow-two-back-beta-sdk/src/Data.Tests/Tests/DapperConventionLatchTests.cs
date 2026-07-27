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
/// <c>SqlNaming.ColumnCase</c> / <c>ParameterCase</c> are static mutable and <c>AddDapperConventions</c> guards its
/// body with an <c>Interlocked</c> latch: the second call succeeds and does nothing. Test <em>order</em> therefore
/// changes the SQL a later test emits, and xUnit parallelises across collections — making it a race, not merely an
/// order dependency. The suite pins parallelism off (see <c>AssemblyInfo.cs</c>) and this case pins the values.
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
    public void SqlNaming_defaults_are_snake_columns_and_camel_parameters()
    {
        SqlNaming.ColumnCase.Should().Be(CaseStyle.Snake);     // every composed fragment names snake_case columns
        SqlNaming.ParameterCase.Should().Be(CaseStyle.Camel);  // …and camelCase parameters
        SqlNaming.Col("OrderLineId").Should().Be("order_line_id");
        SqlNaming.ParRef("OrderLineId").Should().Be("@orderLineId");
    }
}
