using AwesomeAssertions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>History rows take their applied time from the registered <see cref="TimeProvider"/>, not the machine clock.</summary>
public sealed class HistoryClockTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task ApplyPending_ShouldStampAppliedAtFromTheRegisteredClock()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");
        var appliedAt = new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(appliedAt));
        services.AddSqliteConnectionFactory(ConnectionString);
        services.AddDatabaseBespokeMigrations(Workspace.Root, options => options.Provider = DatabaseProvider.Sqlite);
        await using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<IMigrationRunnerService>();

        (await runner.ApplyPendingAsync("test", CancellationToken.None)).ValueOrThrow();

        var status = (await runner.GetStatusAsync(CancellationToken.None)).ValueOrThrow();
        status.Applied.Should().ContainSingle().Which.AppliedAt.Should().Be(appliedAt);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
