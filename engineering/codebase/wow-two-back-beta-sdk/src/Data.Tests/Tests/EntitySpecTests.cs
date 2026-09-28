using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Specs;
using WoW.Two.Sdk.Backend.Beta.Data.Specs;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Provider-neutral entity specs: marker conventions, the EF Core mapping, stamp tokens and unsupported features.</summary>
public sealed class EntitySpecTests
{
    [Fact]
    public void Builder_ShouldFillUnsetPartsFromMarkerInterfaces()
    {
        var spec = new EntitySpecRegistry().Add<Account>(_ => { }).Find(typeof(Account))!;

        spec.Table.Should().Be("accounts");
        spec.Key.Should().Equal("Id");
        spec.Concurrency.Should().BeEquivalentTo(new ConcurrencySpec { Property = "Version", Kind = ConcurrencyTokenKind.Counter });
        spec.TenantProperty.Should().Be("TenantId");
        spec.SoftDeleteProperty.Should().BeNull();
    }

    [Fact]
    public async Task Spec_ShouldShapeTheEfModel_ForTheEntitiesTheContextMaps()
    {
        await using var app = await SpecApp<OrdersContext>.StartAsync(services => services.AddEntitySpecs(typeof(OrderSpec).Assembly));
        await using var scope = app.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<OrdersContext>().Model;

        var order = model.FindEntityType(typeof(Order))!;
        order.GetTableName().Should().Be("sales_orders");
        var number = order.FindProperty(nameof(Order.Number))!;
        (number.GetColumnName(), number.IsNullable, number.GetMaxLength()).Should().Be(("order_no", false, 20));
        var total = order.FindProperty(nameof(Order.Total))!;
        (total.GetPrecision(), total.GetScale()).Should().Be((12, 2));
        var index = order.GetIndexes().Single();
        (index.IsUnique, index.GetDatabaseName()).Should().Be((true, "ux_orders_number"));
        order.FindProperty(nameof(Order.Stamp))!.IsConcurrencyToken.Should().BeTrue();
        order.FindProperty(nameof(Order.Display)).Should().BeNull();
        order.GetDeclaredQueryFilters().Should().NotBeEmpty();
        model.FindEntityType(typeof(Account)).Should().BeNull("a spec never adds an entity the context does not map");
    }

    [Fact]
    public async Task StampToken_ShouldRotateOnSave_AndRejectAStaleWriter()
    {
        await using var app = await SpecApp<OrdersContext>.StartAsync(services => services.AddEntitySpecs(typeof(OrderSpec).Assembly));
        var id = Guid.NewGuid();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrdersContext>();
            context.Add(new Order { Id = id, Number = "A-1", Stamp = "initial" });
            await context.SaveChangesAsync();
        }

        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();
        var mine = await first.ServiceProvider.GetRequiredService<OrdersContext>().Set<Order>().SingleAsync(o => o.Id == id);
        var theirs = await second.ServiceProvider.GetRequiredService<OrdersContext>().Set<Order>().SingleAsync(o => o.Id == id);

        mine.Total = 10m;
        await first.ServiceProvider.GetRequiredService<OrdersContext>().SaveChangesAsync();
        mine.Stamp.Should().NotBe("initial");
        theirs.Total = 20m;
        var stale = () => second.ServiceProvider.GetRequiredService<OrdersContext>().SaveChangesAsync();
        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task XminOffPostgres_ShouldThrow_UnlessTheHostSaysSkip()
    {
        await using (var strict = await SpecApp<LedgerContext>.StartAsync(services => services.AddEntitySpecs(registry => registry.Add<Ledger>(_ => { })), ensureCreated: false))
        {
            await using var scope = strict.Services.CreateAsyncScope();
            var build = () => scope.ServiceProvider.GetRequiredService<LedgerContext>().Model;
            build.Should().Throw<UnsupportedSpecException>().Which.Features.Should().Equal("Ledger.Xmin (Xmin concurrency on Microsoft.EntityFrameworkCore.Sqlite)");
        }

        await using var lenient = await SpecApp<LenientLedgerContext>.StartAsync(
            services => services.AddEntitySpecs(registry => registry.Add<Ledger>(_ => { })),
            new Dictionary<string, string?> { ["Data:Specs:Unsupported"] = "Skip" });
        await using var lenientScope = lenient.Services.CreateAsyncScope();
        lenientScope.ServiceProvider.GetRequiredService<LenientLedgerContext>().Model
            .FindEntityType(typeof(Ledger))!.FindProperty(nameof(Ledger.Xmin))!.IsConcurrencyToken.Should().BeFalse();
    }

    public sealed class Order : IKeyedEntity<Guid>, ISoftDeletable
    {
        public Guid Id { get; set; }

        public string Number { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public string Stamp { get; set; } = string.Empty;

        public string Display => $"#{Number}";

        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }
    }

    public sealed class OrderSpec : IEntitySpecConfiguration<Order>
    {
        public void Configure(EntitySpecBuilder<Order> builder)
        {
            builder.ToTable("sales_orders");
            builder.Property(o => o.Number).HasColumnName("order_no").IsRequired().HasMaxLength(20);
            builder.Property(o => o.Total).HasPrecision(12, 2);
            builder.HasIndex(o => o.Number).IsUnique().HasName("ux_orders_number");
            builder.HasConcurrencyToken(o => o.Stamp, ConcurrencyTokenKind.Stamp);
            builder.Ignore(o => o.Display);
        }
    }

    public sealed class Account : IKeyedEntity<Guid>, IHasTableName, IVersioned, IHasTenant<string>
    {
        public static string TableName => "accounts";

        public Guid Id { get; set; }

        public uint Version { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    public sealed class Ledger : IKeyedEntity<Guid>, IHasXmin
    {
        public Guid Id { get; set; }

        public uint Xmin { get; set; }
    }

    public sealed class OrdersContext(DbContextOptions<OrdersContext> options) : AppDbContextBase(options)
    {
        public DbSet<Order> Orders => Set<Order>();
    }

    public sealed class LedgerContext(DbContextOptions<LedgerContext> options) : AppDbContextBase(options)
    {
        public DbSet<Ledger> Ledgers => Set<Ledger>();
    }

    public sealed class LenientLedgerContext(DbContextOptions<LenientLedgerContext> options) : AppDbContextBase(options)
    {
        public DbSet<Ledger> Ledgers => Set<Ledger>();
    }

    /// <summary>A container with one SQLite in-memory database shared by every scope.</summary>
    private sealed class SpecApp<TContext>(ServiceProvider services, SqliteConnection connection) : IAsyncDisposable
        where TContext : DbContext
    {
        public ServiceProvider Services { get; } = services;

        public static async Task<SpecApp<TContext>> StartAsync(
            Action<IServiceCollection> register,
            Dictionary<string, string?>? configuration = null,
            bool ensureCreated = true)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build());
            services.AddDbContext<TContext>(options => options.UseSqlite(connection));
            register(services);
            var provider = services.BuildServiceProvider();
            if (ensureCreated)
            {
                await using var scope = provider.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
            }

            return new SpecApp<TContext>(provider, connection);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
