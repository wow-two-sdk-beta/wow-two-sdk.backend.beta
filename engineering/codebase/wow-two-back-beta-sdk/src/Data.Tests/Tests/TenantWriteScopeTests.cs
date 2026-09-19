using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;
using WoW.Two.Sdk.Backend.Beta.Tenancy.PerRow;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Verifies server-owned tenant stamping and stored-row write isolation.</summary>
public sealed class TenantWriteScopeTests
{
    [Fact]
    public async Task Interceptor_ShouldOverwriteCreateTenant_AndRejectCrossTenantUpdate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenant = new AmbientTenantContext();
        var options = new DbContextOptionsBuilder<TenantContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options;

        await using (var setup = new TenantContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Entities.Add(new TenantEntity
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                TenantId = "tenant-b",
                Value = "protected",
            });
            await setup.SaveChangesAsync();
        }

        tenant.Set("tenant-a");
        await using (var create = new TenantContext(options))
        {
            var entity = new TenantEntity
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                TenantId = "tenant-b",
                Value = "created",
            };
            create.Entities.Add(entity);
            await create.SaveChangesAsync();
            entity.TenantId.Should().Be("tenant-a");
        }

        await using (var update = new TenantContext(options))
        {
            update.Entities.Update(new TenantEntity
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                TenantId = "tenant-a",
                Value = "stolen",
            });

            var action = () => update.SaveChangesAsync();

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*stored row belongs to another tenant*");
        }
    }

    private sealed record TenantEntity : IKeyedEntity<Guid>, IHasTenant<string>
    {
        public required Guid Id { get; init; }
        public required string TenantId { get; set; }
        public required string Value { get; set; }
    }

    private sealed class TenantContext(DbContextOptions<TenantContext> options) : DbContext(options)
    {
        internal DbSet<TenantEntity> Entities => Set<TenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantEntity>(entity =>
            {
                entity.HasKey(value => value.Id);
                entity.Property(value => value.TenantId).IsRequired();
            });
        }
    }
}
