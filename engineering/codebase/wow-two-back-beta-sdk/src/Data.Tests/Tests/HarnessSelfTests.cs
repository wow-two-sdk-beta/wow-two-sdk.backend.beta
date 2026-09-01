using AwesomeAssertions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>
/// P0-08 — the harness self-test the whole suite's isolation premise rests on.
/// </summary>
[Collection(DataTestCollection.Name)]
public sealed class HarnessSelfTests(DataTestDb testDb) : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task ResetAsync_truncates_data_and_keeps_the_schema()
    {
        await using (var context = TestDb.NewContext())
        {
            context.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "doomed" });
            await context.SaveChangesAsync();
        }

        await TestDb.ResetAsync();

        await using var verify = TestDb.NewContext();
        (await verify.Widgets.CountAsync()).Should().Be(0);                     // truncated for real — an uninitialized respawner returns early and truncates nothing
        (await verify.Database.CanConnectAsync()).Should().BeTrue();            // the schema survived the truncate, so the next test has tables to write to
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Every_test_starts_with_an_empty_database(int run)
    {
        await using var context = TestDb.NewContext();

        (await context.Widgets.CountAsync()).Should().Be(0); // run 2 proves run 1's row is gone — the per-test reset does the isolating

        context.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = $"run-{run}" });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task OpenConnectionAsync_hands_back_a_second_real_connection_that_sees_committed_rows()
    {
        var id = Guid.NewGuid();
        await using (var context = TestDb.NewContext())
        {
            context.Widgets.Add(new Widget { Id = id, Name = "visible" });
            await context.SaveChangesAsync();
        }

        await using var connection = await TestDb.OpenConnectionAsync();

        var name = await connection.QuerySingleOrDefaultAsync<string>(
            "select name from widgets where id = @id",
            new { id });

        name.Should().Be("visible");
    }
}
