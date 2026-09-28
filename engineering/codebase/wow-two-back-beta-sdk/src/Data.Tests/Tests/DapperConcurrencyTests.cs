using Microsoft.EntityFrameworkCore;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Dapper writes honour concurrency tokens: a stale counter or <c>xmin</c> fails the update or delete instead of winning silently.</summary>
[Collection(DataTestCollection.Name)]
public sealed class DapperConcurrencyTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task VersionCounter_ShouldIncrementOnUpdate_AndRejectAStaleWriter()
    {
        await using var source = NpgsqlDataSource.Create(TestDb.ConnectionString);
        var repository = new DapperRepository<VersionedWidget, Guid>(new DataSourceConnectionFactory(source));
        var row = new VersionedWidget { Id = Guid.NewGuid(), Name = "initial" };
        await repository.CreateAsync(row);
        var first = (await repository.GetByIdAsync(row.Id))!;
        var second = (await repository.GetByIdAsync(row.Id))!;

        first.Name = "first";
        await repository.UpdateAsync(first);
        Assert.Equal(1u, first.Version);
        second.Name = "second";
        var conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.UpdateAsync(second));
        Assert.Equal(typeof(VersionedWidget), conflict.EntityType);
        Assert.Equal(row.Id, conflict.Id);

        var stored = (await repository.GetByIdAsync(row.Id))!;
        Assert.Equal(("first", 1u), (stored.Name, stored.Version));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.DeleteAsync(second));
        await repository.DeleteAsync(first);
        Assert.Null(await repository.GetByIdAsync(row.Id));
    }

    [Fact]
    public async Task VersionCounter_ShouldAgreeWithEfCore()
    {
        await using var source = NpgsqlDataSource.Create(TestDb.ConnectionString);
        var repository = new DapperRepository<VersionedWidget, Guid>(new DataSourceConnectionFactory(source));
        var row = new VersionedWidget { Id = Guid.NewGuid(), Name = "initial" };
        await repository.CreateAsync(row);
        var stale = (await repository.GetByIdAsync(row.Id))!;

        await using (var context = TestDb.NewContext())
        {
            var tracked = await context.Set<VersionedWidget>().SingleAsync(widget => widget.Id == row.Id);
            tracked.Name = "ef";
            await context.SaveChangesAsync();
            Assert.Equal(1u, tracked.Version);
        }

        stale.Name = "dapper";
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.UpdateAsync(stale));
        var fresh = (await repository.GetByIdAsync(row.Id))!;
        fresh.Name = "dapper";
        await repository.UpdateAsync(fresh);

        await using var verify = TestDb.NewContext();
        var read = await verify.Set<VersionedWidget>().AsNoTracking().SingleAsync(widget => widget.Id == row.Id);
        Assert.Equal(("dapper", 2u), (read.Name, read.Version));
    }

    [Fact]
    public async Task Xmin_ShouldBeReadBackOnUpdate_AndRejectAStaleWriter()
    {
        await using var source = NpgsqlDataSource.Create(TestDb.ConnectionString);
        var repository = new DapperRepository<XminWidget, Guid>(new DataSourceConnectionFactory(source));
        var row = new XminWidget { Id = Guid.NewGuid(), Name = "initial" };
        await repository.CreateAsync(row);
        var first = (await repository.GetByIdAsync(row.Id))!;
        var stale = (await repository.GetByIdAsync(row.Id))!;
        var readXmin = first.Xmin;

        first.Name = "first";
        await repository.UpdateAsync(first);
        Assert.NotEqual(readXmin, first.Xmin);
        Assert.Equal(first.Xmin, (await repository.GetByIdAsync(row.Id))!.Xmin);
        first.Name = "again";
        await repository.UpdateAsync(first);

        stale.Name = "stale";
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.UpdateAsync(stale));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.DeleteAsync(stale));
        Assert.Equal("again", (await repository.GetByIdAsync(row.Id))!.Name);
        await repository.DeleteAsync(first);
        Assert.False(await repository.ExistsAsync(row.Id));
    }
}
