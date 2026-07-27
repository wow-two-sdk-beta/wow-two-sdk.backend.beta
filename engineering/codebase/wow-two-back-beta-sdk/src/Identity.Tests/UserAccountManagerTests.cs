using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests;

/// <summary>Identity core vertical — create → find → delete a user, and case-insensitive duplicate rejection, over in-memory SQLite.</summary>
public sealed class UserAccountManagerTests
{
    private sealed class TestIdentityDbContext(DbContextOptions options) : AppDbContextBase(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyIdentitySchema<IdentityUser, IdentityRole, Guid>();
        }
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestIdentityDbContext>(o => o.UseSqlite(connection));
        services.AddUserAccounts<IdentityUser>(o => o.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<TestIdentityDbContext>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>().Database.EnsureCreated();
        return provider;
    }

    [Fact]
    public async Task Creates_finds_and_deletes_a_user()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserAccountManager<IdentityUser, Guid>>();

        var user = new IdentityUser { UserName = "alice", Email = "alice@example.test" };
        (await manager.CreateAsync(user)).Succeeded.Should().BeTrue();
        user.Id.Should().NotBe(Guid.Empty);            // store-generated
        user.NormalizedUserName.Should().Be("ALICE");
        user.SecurityStamp.Should().NotBeNullOrEmpty();

        (await manager.FindByIdAsync(user.Id)).Should().NotBeNull();
        (await manager.FindByNameAsync("ALICE")).Should().NotBeNull();               // case-insensitive
        (await manager.FindByEmailAsync("ALICE@EXAMPLE.TEST")).Should().NotBeNull();

        (await manager.DeleteAsync(user)).Succeeded.Should().BeTrue();
        (await manager.FindByIdAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Rejects_duplicate_user_name_case_insensitively()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserAccountManager<IdentityUser, Guid>>();

        (await manager.CreateAsync(new IdentityUser { UserName = "bob", Email = "bob@x.test" })).Succeeded.Should().BeTrue();
        var duplicate = await manager.CreateAsync(new IdentityUser { UserName = "BOB", Email = "bob2@x.test" });

        duplicate.Succeeded.Should().BeFalse();
        duplicate.Errors.Should().Contain(e => e.Code == "DuplicateUserName");
    }
}
