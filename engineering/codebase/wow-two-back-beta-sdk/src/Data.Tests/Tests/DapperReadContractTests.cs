using Microsoft.EntityFrameworkCore;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

[Collection(DataTestCollection.Name)]
public sealed class DapperReadContractTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task AllGeneratedReadsExcludeSoftDeletedRows()
    {
        await using var source = NpgsqlDataSource.Create(TestDb.ConnectionString);
        var factory = new DataSourceConnectionFactory(source);
        var repository = new DapperRepository<SoftWidget, Guid>(factory);
        var alive = new SoftWidget { Id = Guid.NewGuid(), Name = "alive" };
        var deleted = new SoftWidget { Id = Guid.NewGuid(), Name = "deleted", IsDeleted = true };
        await repository.CreateRangeAsync([alive, deleted]);
        Assert.Single(await repository.GetAllAsync());
        Assert.Equal(1, await repository.CountAsync());
        Assert.Null(await repository.GetByIdAsync(deleted.Id));
        Assert.False(await repository.ExistsAsync(deleted.Id));
        Assert.NotNull(await repository.GetByIdAsync(alive.Id));
        Assert.True(await repository.ExistsAsync(alive.Id));
        var administrative = new IncludingDeletedRepository(factory);
        Assert.Equal(2, await administrative.CountAsync());
        Assert.NotNull(await administrative.GetByIdAsync(deleted.Id));
    }

    [Fact]
    public async Task DapperReadCarriesXminIntoEfConcurrencyChecks()
    {
        await using var source = NpgsqlDataSource.Create(TestDb.ConnectionString);
        var repository = new DapperRepository<XminWidget, Guid>(new DataSourceConnectionFactory(source));
        var row = new XminWidget { Id = Guid.NewGuid(), Name = "initial" };
        await repository.CreateAsync(row);
        XminWidget read = (await repository.GetByIdAsync(row.Id))!;
        Assert.NotEqual(0u, read.Xmin);
        Assert.Equal(read.Xmin, (await repository.GetAllAsync()).Single().Xmin);

        await using var context = TestDb.NewContext();
        context.Attach(read);
        read.Name = "ef-write";
        await context.SaveChangesAsync();
        Assert.Equal("ef-write", (await repository.GetByIdAsync(row.Id))!.Name);

        XminWidget stale = (await repository.GetByIdAsync(row.Id))!;
        read.Name = "concurrent";
        await context.SaveChangesAsync();
        await using var other = TestDb.NewContext();
        other.Attach(stale);
        stale.Name = "stale";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
    }

    private sealed class IncludingDeletedRepository(IDbConnectionFactory factory)
        : DapperRepository<SoftWidget, Guid>(factory)
    {
        protected override bool IncludeSoftDeleted => true;
    }
}
