using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Sessions;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.DataUnits;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using WoW.Two.Sdk.Backend.Beta.Mediator.Result;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

[Collection(DataTestCollection.Name)]
public sealed class TransactionalRequestTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task CommittedRequestReplaysAcrossScopesWithoutRepeatingWrites()
    {
        await using var provider = Build();
        await Send(provider, new CreateWidget("same"));
        await using var observer = TestDb.NewContext();
        Assert.Single(await observer.Widgets.ToListAsync());
        await Send(provider, new CreateWidget("same"));
        Assert.Single(await observer.Widgets.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRequestRollsBackAndAllowsRetry(bool throws)
    {
        await using var provider = Build();
        var failed = await Send(provider, new CreateWidget("retry") { Fail = true, Throw = throws });
        Assert.False(failed.IsSuccess);
        await using var observer = TestDb.NewContext();
        Assert.Empty(await observer.Widgets.ToListAsync());
        Assert.True((await Send(provider, new CreateWidget("retry"))).IsSuccess);
        Assert.Single(await observer.Widgets.ToListAsync());
    }

    [Fact]
    public async Task OuterRollbackDiscardsNestedSuccessAndItsReplay()
    {
        await using var provider = Build();
        await using (var scope = provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
            await using (await session.BeginAsync())
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                Assert.True((await sender.SendAsync(new CreateWidget("nested"))).IsSuccess);
                await using var observer = TestDb.NewContext();
                Assert.Empty(await observer.Widgets.ToListAsync());
            }
        }
        Assert.True((await Send(provider, new CreateWidget("nested"))).IsSuccess);
        await using var check = TestDb.NewContext();
        Assert.Single(await check.Widgets.ToListAsync());
    }

    [Fact]
    public async Task SlowCallbackDoesNotPreventIdempotencyCompletion()
    {
        await using var provider = Build(options => options.HookTimeout = TimeSpan.FromMilliseconds(50));
        await using (var scope = provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
            await using var outer = await session.BeginAsync();
            await session.OnCommittedAsync(token => new ValueTask(Task.Delay(Timeout.Infinite, token)));
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Assert.True((await sender.SendAsync(new CreateWidget("hook"))).IsSuccess);
            await outer.CompleteAsync();
        }
        Assert.True((await Send(provider, new CreateWidget("hook"))).IsSuccess);
        await using var observer = TestDb.NewContext();
        Assert.Single(await observer.Widgets.ToListAsync());
    }

    private ServiceProvider Build(Action<DataSessionOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DataTestDbContext>(TestDb.ApplyProvider);
        services.AddDataSession<DataTestDbContext>(configure);
        services.AddMediator(typeof(TransactionalRequestTests).Assembly);
        services.AddMediatorDataUnitInterceptor();
        services.AddMediatorDeduplicatingInterceptor();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public async Task UncertainCommitNeverReleasesOwnershipOrClaimsRollback()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DataTestDbContext>(options =>
        {
            TestDb.ApplyProvider(options);
            options.AddInterceptors(new LostCommitAcknowledgmentInterceptor());
        });
        services.AddDataSession<DataTestDbContext>();
        services.AddMemoryCache();
        services.AddSingleton<IIdempotencyRepository, InMemoryIdempotencyRepository>();
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IIdempotencyRepository>();
        bool rolledBack = false;
        await using (var scope = provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDataSession>();
            var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
            var behavior = new DeduplicatingInterceptor<CreateWidget, int>(store, session);
            await using (var unit = await session.BeginAsync())
            {
                session.OnRolledBack(_ => { rolledBack = true; return ValueTask.CompletedTask; });
                await behavior.HandleAsync(new CreateWidget("uncertain"), () =>
                {
                    context.Widgets.Add(new Widget
                    {
                        Id = Guid.NewGuid(), Name = "uncertain",
                        CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch
                    });
                    return ValueTask.FromResult(1);
                }, CancellationToken.None);
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => unit.CompleteAsync().AsTask());
                Assert.Equal("Lost commit acknowledgment", error.Message);
            }
            Assert.Equal(DataSessionState.Faulted, session.State);
        }
        Assert.False(rolledBack);
        await using var observer = TestDb.NewContext();
        Assert.Single(await observer.Widgets.ToListAsync());
        var retry = new DeduplicatingInterceptor<CreateWidget, int>(store);
        await Assert.ThrowsAsync<AppException>(() =>
            retry.HandleAsync(new CreateWidget("uncertain"), () => ValueTask.FromResult(2), CancellationToken.None).AsTask());
    }

    private sealed class LostCommitAcknowledgmentInterceptor : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Lost commit acknowledgment");
        }
    }

    private static async Task<AppResult<Guid>> Send(ServiceProvider provider, CreateWidget request)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(request);
    }

    public sealed record CreateWidget(string IdempotencyKey)
        : IRequest<AppResult<Guid>>, ITransactionalRequest, IIdempotent
    {
        public bool Fail { get; init; }
        public bool Throw { get; init; }
    }

    public sealed class CreateWidgetHandler(DataTestDbContext context) : IRequestHandler<CreateWidget, AppResult<Guid>>
    {
        public async ValueTask<AppResult<Guid>> HandleAsync(CreateWidget request, CancellationToken cancellationToken)
        {
            var widget = new Widget
            {
                Id = Guid.NewGuid(), Name = request.IdempotencyKey,
                CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch
            };
            context.Widgets.Add(widget);
            await context.SaveChangesAsync(cancellationToken);
            if (request.Throw)
            {
                throw AppErrorFactory.Conflict("reject").ToException();
            }
            return request.Fail ? AppResult<Guid>.Fail(AppErrorFactory.Conflict("reject")) : AppResult<Guid>.Ok(widget.Id);
        }
    }
}
