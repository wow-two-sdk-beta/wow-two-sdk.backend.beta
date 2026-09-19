using AwesomeAssertions;
using Dapper;
using Microsoft.Data.Sqlite;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>Verifies SQLite constraint evolution through an explicit table rebuild.</summary>
public sealed class SqliteConstraintEvolutionTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task TableRebuild_ShouldPreserveRowsIndexesTriggersAndForeignKeysAcrossApplyAndRollback()
    {
        Workspace.Write("001-baseline", """
            create table parent(id integer primary key);
            create table audit(item_id integer not null);
            create table item(
                id integer primary key,
                parent_id integer not null references parent(id),
                state text not null check(state in ('draft', 'published')));
            create index ix_item_parent on item(parent_id);
            create trigger tr_item_audit after insert on item
                begin insert into audit(item_id) values (new.id); end;
            insert into parent(id) values (1);
            insert into item(id, parent_id, state) values (1, 1, 'draft');
            """, "drop table item; drop table audit; drop table parent;");
        Workspace.Write("002-add-archived", RebuildSql("'draft', 'published', 'archived'"), """
            delete from item where state = 'archived';
            """ + RebuildSql("'draft', 'published'"));

        await using var migrator = CreateMigrator(options => options.AllowRollback = true);
        (await migrator.Runner.ApplyPendingAsync("apply", CancellationToken.None)).ValueOrThrow();

        await using (var connection = await migrator.OpenConnectionAsync())
        {
            await connection.ExecuteAsync(
                "insert into item(id, parent_id, state) values (2, 1, 'archived')");
            await AssertPreservedAsync(connection);
            (await connection.ExecuteScalarAsync<long>(
                "select count(*) from audit where item_id = 2")).Should().Be(1);
        }

        (await migrator.Runner.RollbackAsync(null, CancellationToken.None))
            .ValueOrThrow().Should().Equal("002-add-archived");

        await using var rolledBack = await migrator.OpenConnectionAsync();
        await AssertPreservedAsync(rolledBack);
        (await rolledBack.ExecuteScalarAsync<long>("select count(*) from item where id = 1")).Should().Be(1);
        var rejected = async () => await rolledBack.ExecuteAsync(
            "insert into item(id, parent_id, state) values (3, 1, 'archived')");
        await rejected.Should().ThrowAsync<SqliteException>();
    }

    private static string RebuildSql(string allowedStates) => $$"""
        create table item_new(
            id integer primary key,
            parent_id integer not null references parent(id),
            state text not null check(state in ({{allowedStates}})));
        insert into item_new(id, parent_id, state) select id, parent_id, state from item;
        drop table item;
        alter table item_new rename to item;
        create index ix_item_parent on item(parent_id);
        create trigger tr_item_audit after insert on item
            begin insert into audit(item_id) values (new.id); end;
        """;

    private static async Task AssertPreservedAsync(System.Data.Common.DbConnection connection)
    {
        (await connection.ExecuteScalarAsync<long>(
            "select count(*) from sqlite_master where type = 'index' and name = 'ix_item_parent'"))
            .Should().Be(1);
        (await connection.ExecuteScalarAsync<long>(
            "select count(*) from sqlite_master where type = 'trigger' and name = 'tr_item_audit'"))
            .Should().Be(1);
        (await connection.ExecuteScalarAsync<long>("select count(*) from pragma_foreign_key_list('item')"))
            .Should().Be(1);
        (await connection.ExecuteScalarAsync<string>("pragma integrity_check")).Should().Be("ok");
    }
}
