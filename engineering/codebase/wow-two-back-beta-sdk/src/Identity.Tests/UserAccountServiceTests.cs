using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests;

/// <summary>Identity core vertical — create → find → delete a user, and case-insensitive duplicate rejection, over in-memory SQLite.</summary>
public sealed class UserAccountServiceTests
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
        var service = scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>();

        var user = new IdentityUser { UserName = "alice", Email = "alice@example.test" };
        (await service.CreateAsync(user)).Succeeded.Should().BeTrue();
        user.Id.Should().NotBe(Guid.Empty);            // store-generated
        user.NormalizedUserName.Should().Be("ALICE");
        user.SecurityStamp.Should().NotBeNullOrEmpty();

        (await service.FindByIdAsync(user.Id)).Should().NotBeNull();
        (await service.FindByNameAsync("ALICE")).Should().NotBeNull();               // case-insensitive
        (await service.FindByEmailAsync("ALICE@EXAMPLE.TEST")).Should().NotBeNull();

        (await service.DeleteAsync(user)).Succeeded.Should().BeTrue();
        (await service.FindByIdAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Rejects_duplicate_user_name_case_insensitively()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>();

        (await service.CreateAsync(new IdentityUser { UserName = "bob", Email = "bob@x.test" })).Succeeded.Should().BeTrue();
        var duplicate = await service.CreateAsync(new IdentityUser { UserName = "BOB", Email = "bob2@x.test" });

        duplicate.Succeeded.Should().BeFalse();
        duplicate.Errors.Should().Contain(e => e.Code == "DuplicateUserName");
    }

    [Fact]
    public async Task Repository_rejects_a_replacement_while_the_original_user_is_tracked()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository<IdentityUser, Guid>>();
        var original = new IdentityUser { UserName = "tracked", Email = "tracked@example.test" };
        (await service.CreateAsync(original)).Succeeded.Should().BeTrue();
        var replacement = new IdentityUser
        {
            Id = original.Id,
            UserName = original.UserName,
            NormalizedUserName = original.NormalizedUserName,
            Email = "changed@example.test",
            NormalizedEmail = "CHANGED@EXAMPLE.TEST",
            SecurityStamp = original.SecurityStamp,
            ConcurrencyStamp = original.ConcurrencyStamp,
        };

        var act = () => repository.UpdateAsync(replacement);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Apply accepted changes to the tracked instance*");
    }
}
