using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;
using WoW.Two.Sdk.Backend.Beta.Data.Sessions;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

[Collection(DataTestCollection.Name)]
public sealed class DataSessionTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EfAndDapperShareVisibilityAndRollback(bool pooled)
    {
        await using var provider = Build(pooled);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Widget, Guid>>();
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
        await using (await session.BeginAsync())
        {
            context.Widgets.Add(Widget("ef"));
            await context.SaveChangesAsync();
            Assert.Single(await repository.GetAllAsync());
            await repository.CreateAsync(Widget("dapper"));
            Assert.Equal(2, await repository.CountAsync());
            await using var observer = TestDb.NewContext();
            Assert.Empty(await observer.Widgets.ToListAsync());
        }
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.Widgets.ToListAsync());
        Assert.Equal(DataSessionState.RolledBack, session.State);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitHooksObserveCommittedRowsAndIsolateFailures(bool pooled)
    {
        await using var provider = Build(pooled);
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        await using var unit = await session.BeginAsync();
        context.Widgets.Add(Widget("pending"));
        int observed = 0;
        await session.OnCommittedAsync(_ => throw new InvalidOperationException("broken callback"));
        await session.OnCommittedAsync(async token =>
        {
            await using var observer = TestDb.NewContext();
            observed = await observer.Widgets.CountAsync(token);
        });
        await unit.CompleteAsync();
        Assert.Equal(1, observed);
        Assert.Equal(DataSessionState.Committed, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.BeginAsync().AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedRollbackPreservesParentWritesAndHooks(bool pooled)
    {
        await using var provider = Build(pooled);
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        var events = new List<string>();
        await using var outer = await session.BeginAsync();
        context.Widgets.Add(Widget("outer"));
        await session.OnCommittedAsync(_ => { events.Add("outer"); return ValueTask.CompletedTask; }, "same");
        await using (var inner = await session.BeginAsync())
        {
            context.Widgets.Add(Widget("inner"));
            await context.SaveChangesAsync();
            await session.OnCommittedAsync(_ => { events.Add("duplicate"); return ValueTask.CompletedTask; }, "same");
            await session.OnCommittedAsync(_ => { events.Add("discard"); return ValueTask.CompletedTask; });
            session.OnRolledBack(_ => { events.Add("rollback"); return ValueTask.CompletedTask; });
            await inner.AbandonAsync();
        }
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(1, session.Depth);
        await outer.CompleteAsync();
        Assert.Equal("rollback,outer", string.Join(',', events));
        await using var observer = TestDb.NewContext();
        Assert.Equal("outer", (await observer.Widgets.SingleAsync()).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedCompletionStillRollsBackWithOuter(bool pooled)
    {
        await using var provider = Build(pooled);
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        bool committed = false;
        bool rolledBack = false;
        await using (await session.BeginAsync())
        {
            await using var inner = await session.BeginAsync();
            context.Widgets.Add(Widget("nested"));
            await session.OnCommittedAsync(_ => { committed = true; return ValueTask.CompletedTask; });
            session.OnRolledBack(_ => { rolledBack = true; return ValueTask.CompletedTask; });
            await inner.CompleteAsync();
            Assert.False(committed);
        }
        Assert.False(committed);
        Assert.True(rolledBack);
        await using var observer = TestDb.NewContext();
        Assert.Empty(await observer.Widgets.ToListAsync());
    }

    [Fact]
    public async Task UnitsRejectWrongOrderAndOutstandingLeases()
    {
        await using var provider = Build(false);
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        await using var outer = await session.BeginAsync();
        await using (var inner = await session.BeginAsync())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => outer.CompleteAsync().AsTask());
            await inner.CompleteAsync();
        }
        await using (var lease = await session.OpenConnectionAsync(factory))
        {
            Assert.False(lease.OwnsConnection);
            Assert.NotNull(lease.Transaction);
            await Assert.ThrowsAsync<InvalidOperationException>(() => outer.CompleteAsync().AsTask());
            Assert.Equal(1, await lease.Connection.ExecuteScalarAsync<int>(
                new CommandDefinition("SELECT 1", transaction: lease.Transaction)));
        }
        await outer.CompleteAsync();
    }

    [Fact]
    public async Task IdleRepositoryRemainsAutonomousAndSessionRejectsRootResolution()
    {
        await using var provider = Build(false);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IDataSession>());
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Widget, Guid>>();
        await repository.CreateAsync(Widget("autonomous"));
        await using var observer = TestDb.NewContext();
        Assert.Single(await observer.Widgets.ToListAsync());
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        Assert.Equal(DataSessionState.Idle, session.State);
    }

    [Fact]
    public async Task CanceledRequestStillRollsBackAndRunsCompensation()
    {
        await using var provider = Build(false);
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        bool rolledBack = false;
        await using (var unit = await session.BeginAsync())
        {
            context.Widgets.Add(Widget("cancel"));
            await context.SaveChangesAsync();
            session.OnRolledBack(token =>
            {
                Assert.False(token.IsCancellationRequested);
                rolledBack = true;
                return ValueTask.CompletedTask;
            });
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unit.CompleteAsync(canceled.Token).AsTask());
        }
        Assert.True(rolledBack);
        await using var observer = TestDb.NewContext();
        Assert.Empty(await observer.Widgets.ToListAsync());
    }

    [Fact]
    public async Task FactoryForAnotherDatabaseIsRefused()
    {
        await using var provider = Build(false);
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
        await using var unit = await session.BeginAsync();
        await using var other = NpgsqlDataSource.Create("Host=localhost;Database=other");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.OpenConnectionAsync(new DataSourceConnectionFactory(other)).AsTask());
    }

    [Fact]
    public async Task RetryingProviderIsRejectedBeforeAnyConnectionOpens()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DataTestDbContext>(options => options.UseNpgsql(
            TestDb.ConnectionString,
            provider => provider.EnableRetryOnFailure()));
        services.AddDataSession<DataTestDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IDataSession>());
        Assert.Contains("retrying", exception.Message);
    }

    [Fact]
    public async Task PoolReuseDoesNotCarryTransactionOrHooksToAnotherScope()
    {
        await using var provider = Build(true);
        DataTestDbContext? first;
        bool leaked = false;
        await using (var scope = provider.CreateAsyncScope())
        {
            first = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
            var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
            _ = await session.BeginAsync();
            first.Widgets.Add(Widget("rollback-on-scope-disposal"));
            await first.SaveChangesAsync();
            await session.OnCommittedAsync(_ => { leaked = true; return ValueTask.CompletedTask; });
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var second = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
            Assert.Same(first, second);
            var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
            Assert.Equal(DataSessionState.Idle, session.State);
            await using var unit = await session.BeginAsync();
            await unit.CompleteAsync();
        }
        Assert.False(leaked);
        await using var observer = TestDb.NewContext();
        Assert.Empty(await observer.Widgets.ToListAsync());
    }

    private ServiceProvider Build(bool pooled)
    {
        var services = new ServiceCollection();
        if (pooled)
        {
            services.AddDbContextPool<DataTestDbContext>(TestDb.ApplyProvider, poolSize: 1);
        }
        else
        {
            services.AddDbContext<DataTestDbContext>(TestDb.ApplyProvider);
        }
        services.AddSingleton(_ => NpgsqlDataSource.Create(TestDb.ConnectionString));
        services.AddSingleton<IDbConnectionFactory>(provider =>
            new DataSourceConnectionFactory(provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddDataSession<DataTestDbContext>();
        services.AddDapperRepository<Widget, Guid>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false });
    }

    private static Widget Widget(string name)
    {
        return new Widget
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch
        };
    }
}
