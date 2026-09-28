using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>An identity registration over in-memory SQLite with a controllable clock.</summary>
internal sealed class AccountsHost : IAsyncDisposable
{
    public const string SigningKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private readonly SqliteConnection _connection;

    private AccountsHost(SqliteConnection connection, ServiceProvider provider, FakeTimeProvider time)
    {
        _connection = connection;
        Provider = provider;
        Time = time;
    }

    public ServiceProvider Provider { get; }

    public FakeTimeProvider Time { get; }

    public static AccountsHost Create(
        Action<IdentityBuilder<IdentityUser, Guid>> slices,
        Action<IdentityCoreOptions>? options = null,
        Action<IServiceCollection>? services = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero));

        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddSingleton<TimeProvider>(time);
        collection.AddDbContext<AccountsDbContext>(o => o.UseSqlite(connection));
        slices(collection.AddUserAccounts<IdentityUser>(options).AddEntityFrameworkStores<AccountsDbContext>());
        services?.Invoke(collection);

        var provider = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.EnsureCreated();

        return new AccountsHost(connection, provider, time);
    }

    public AsyncServiceScope Scope() => Provider.CreateAsyncScope();

    public async ValueTask DisposeAsync()
    {
        await Provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>Creates and persists a user through the core service.</summary>
    public async Task<IdentityUser> CreateUserAsync(string userName, string? email = null)
    {
        await using var scope = Scope();
        var user = new IdentityUser { UserName = userName, Email = email ?? $"{userName}@example.test" };
        var result = await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Errors[0].Description);

        return user;
    }

    /// <summary>Reloads a user in a fresh scope, as the next request would see it.</summary>
    public async Task<IdentityUser> ReloadAsync(Guid id)
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(id)
            ?? throw new InvalidOperationException($"User {id} is missing.");
    }
}

/// <summary>Hosts the identity schema for the account-slice tests.</summary>
internal sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : AppDbContextBase(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyIdentitySchema<IdentityUser, IdentityRole, Guid>();
        modelBuilder.ApplyDateTimeOffsetToBinaryConversion();
    }
}
