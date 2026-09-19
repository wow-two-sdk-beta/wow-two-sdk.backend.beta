using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>Verifies migration sources return actionable errors for incomplete migration pairs.</summary>
public sealed class MigrationBrokerTests
{
    [Fact]
    public void File_system_broker_returns_migration_when_pair_is_complete()
    {
        var root = CreateMigrationFolder(includeRollback: true);
        try
        {
            var result = new FileSystemMigrationBroker(root).Read();

            result.IsFailure(out _, out var migrations).Should().BeFalse();
            migrations.Should().ContainSingle()
                .Which.Name.Should().Be("001-baseline");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void File_system_broker_returns_file_not_found_when_rollback_is_missing()
    {
        var root = CreateMigrationFolder(includeRollback: false);
        try
        {
            var result = new FileSystemMigrationBroker(root).Read();

            result.IsFailure(out var error, out _).Should().BeTrue();
            error!.Type.Should().Be(AppErrorType.FileNotFound);
            error.Message.Should().ContainAll("001-baseline", "Rollback.sql");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Embedded_broker_returns_file_not_found_when_rollback_is_missing()
    {
        var result = new EmbeddedResourceMigrationBroker(typeof(MigrationBrokerTests).Assembly).Read();

        result.IsFailure(out var error, out _).Should().BeTrue();
        error!.Type.Should().Be(AppErrorType.FileNotFound);
        error.Message.Should().ContainAll("002-missing", "Rollback.sql");
    }

    private static string CreateMigrationFolder(bool includeRollback)
    {
        var root = Path.Combine(Path.GetTempPath(), $"wow-two-migrations-{Guid.NewGuid():N}");
        var migration = Path.Combine(root, "001-baseline");
        Directory.CreateDirectory(migration);
        File.WriteAllText(Path.Combine(migration, "Apply.sql"), "create table sample (id int);");

        if (includeRollback)
        {
            File.WriteAllText(Path.Combine(migration, "Rollback.sql"), "drop table sample;");
        }

        return root;
    }
}
