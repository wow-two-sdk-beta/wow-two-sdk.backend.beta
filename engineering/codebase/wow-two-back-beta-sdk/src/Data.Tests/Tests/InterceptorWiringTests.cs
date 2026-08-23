using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>
/// P0-01 / P0-02 / P0-03 — what actually ends up on the resolved <see cref="DbContextOptions"/> of the flagship
/// <c>AddPostgresPersistence</c> registration.
/// </summary>
/// <remarks>
/// D5 / D7: <c>AddPostgresPersistence</c> registers its context with <c>AddDbContext</c> directly, so unless it runs
/// the <c>AddEfInterceptor</c> auto-wire loop, every pluggable interceptor — hooks, guards, outbox — is silently
/// dropped. There is no warning; the concern simply never happens. These cases resolve the context from DI (never
/// <c>RelationalTestDb.NewContext</c>, which builds options directly and would bypass the very code under test).
/// </remarks>
[Collection(DataTestCollection.Name)]
public sealed class InterceptorWiringTests(DataTestDb testDb) : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    // A name no machine sets, so an ambient DB_CONNECTION on a developer box cannot silently repoint the suite.
    private const string UnusedConnectionEnvironmentVariable = "WOW_TWO_DATA_TESTS_NO_SUCH_ENV_VAR";

    [Fact]
    public async Task An_interceptor_registered_via_AddEfInterceptor_fires_under_AddPostgresPersistence()
    {
        var log = new InterceptorLog();
        await using var provider = BuildFlagshipProvider(log);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        context.Widgets.Add(NewWidget());
        await context.SaveChangesAsync();

        log.CountOf(FirstRecordingInterceptor.Name).Should().Be(1); // the flagship registration ran the auto-wire loop — D5/D7 closed
    }

    [Fact]
    public async Task An_interceptor_registered_via_AddEfInterceptor_fires_under_AddEntityFrameworkCore()
    {
        var log = new InterceptorLog();
        await using var provider = BuildEntityFrameworkCoreProvider(log);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        context.Widgets.Add(NewWidget());
        await context.SaveChangesAsync();

        log.CountOf(FirstRecordingInterceptor.Name).Should().Be(1); // control for the case above: the seam itself works, so a failure there is AddPostgresPersistence's
    }

    [Fact]
    public async Task Interceptors_fire_in_DI_registration_order()
    {
        var log = new InterceptorLog();
        await using var provider = BuildFlagshipProvider(log, services => services.AddEfInterceptor<SecondRecordingInterceptor>());

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataTestDbContext>();
        context.Widgets.Add(NewWidget());
        await context.SaveChangesAsync();

        log.Entries
            .Where(entry => entry is FirstRecordingInterceptor.Name or SecondRecordingInterceptor.Name)
            .Should()
            .Equal(FirstRecordingInterceptor.Name, SecondRecordingInterceptor.Name); // order is pinned, not luck of registration — a guard must observe entries before soft-delete flips state
    }

    [Fact]
    public async Task Audit_and_soft_delete_are_attached_exactly_once()
    {
        var log = new InterceptorLog();
        await using var provider = BuildFlagshipProvider(log, services => services.AddEfCoreSoftDeleteFilter());

        using var scope = provider.CreateScope();
        var interceptors = ResolvedInterceptors(scope.ServiceProvider);

        interceptors.OfType<AuditInterceptor>().Should().ContainSingle();      // one arrival only — a second would stamp UpdatedAt twice, silently
        interceptors.OfType<SoftDeleteInterceptor>().Should().ContainSingle(); // and re-enter the delete rewrite
    }

    [Fact]
    public async Task The_pluggable_interceptor_is_the_same_singleton_instance_DI_holds()
    {
        var log = new InterceptorLog();
        await using var provider = BuildFlagshipProvider(log);

        using var scope = provider.CreateScope();
        var fromDi = scope.ServiceProvider.GetRequiredService<FirstRecordingInterceptor>();

        ResolvedInterceptors(scope.ServiceProvider)
            .Should()
            .Contain(fromDi); // the options carry the DI singleton, not a second copy with its own state
    }

    private static IReadOnlyList<IInterceptor> ResolvedInterceptors(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<DbContextOptions<DataTestDbContext>>();
        return [.. options.FindExtension<CoreOptionsExtension>()?.Interceptors ?? []];
    }

    private static Widget NewWidget() => new() { Id = Guid.NewGuid(), Name = "wired" };

    private ServiceProvider BuildFlagshipProvider(InterceptorLog log, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = TestDb.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddEfInterceptor<FirstRecordingInterceptor>();
        configure?.Invoke(services);
        services.AddPostgresPersistence<DataTestDbContext>(
            configuration,
            options => options.ConnectionStringEnvironmentVariable = UnusedConnectionEnvironmentVariable);

        return services.BuildServiceProvider();
    }

    private ServiceProvider BuildEntityFrameworkCoreProvider(InterceptorLog log)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddEfInterceptor<FirstRecordingInterceptor>();
        services.AddTestEntityFrameworkCore(TestDb);
        return services.BuildServiceProvider();
    }
}
