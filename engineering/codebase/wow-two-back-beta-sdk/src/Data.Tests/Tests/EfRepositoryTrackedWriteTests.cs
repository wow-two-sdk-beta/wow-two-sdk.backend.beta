using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Repositories;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

public sealed class EfRepositoryTrackedWriteTests
{
    private sealed record TrackedEntity : IKeyedEntity<Guid>, IVersioned
    {
        public required Guid Id { get; init; }
        public required string Name { get; set; }
        public uint Version { get; set; }
    }

    private sealed class TrackingContext : DbContext
    {
        public TrackingContext()
            : base(new DbContextOptionsBuilder<TrackingContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TrackedEntity>(entity =>
            {
                entity.HasKey(value => value.Id);
                entity.Property(value => value.Version).IsConcurrencyToken();
            });
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            ChangeTracker.DetectChanges();
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task UpdateAsync_ShouldPreserveTrackedOriginalsAndPropertyChanges()
    {
        await using var context = new TrackingContext();
        var entity = new TrackedEntity { Id = Guid.NewGuid(), Name = "before", Version = 7 };
        context.Attach(entity);
        entity.Name = "after";
        var repository = new EfRepository<TrackedEntity, Guid>(context);

        await repository.UpdateAsync(entity);

        var entry = context.Entry(entity);
        entry.Property(value => value.Name).IsModified.Should().BeTrue();
        entry.Property(value => value.Id).IsModified.Should().BeFalse();
        entry.Property(value => value.Version).OriginalValue.Should().Be(7U);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectAReplacementWhileItsOriginalIsTracked()
    {
        await using var context = new TrackingContext();
        var original = new TrackedEntity { Id = Guid.NewGuid(), Name = "before", Version = 7 };
        context.Attach(original);
        var replacement = original with { Name = "after" };
        var repository = new EfRepository<TrackedEntity, Guid>(context);

        var act = () => repository.UpdateAsync(replacement);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Apply accepted changes to the tracked instance*");
        context.Entry(original).State.Should().Be(EntityState.Unchanged);
        context.Entry(replacement).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSupportAnExplicitDetachedFullStateWrite()
    {
        await using var context = new TrackingContext();
        var detached = new TrackedEntity { Id = Guid.NewGuid(), Name = "after", Version = 7 };
        var repository = new EfRepository<TrackedEntity, Guid>(context);

        await repository.UpdateAsync(detached);

        var entry = context.Entry(detached);
        entry.State.Should().Be(EntityState.Modified);
        entry.Property(value => value.Name).IsModified.Should().BeTrue();
        entry.Property(value => value.Version).OriginalValue.Should().Be(7U);
    }
}
