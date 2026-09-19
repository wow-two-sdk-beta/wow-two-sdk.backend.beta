using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>Verifies SQLite's explicit single-applicant coordination boundary.</summary>
public sealed class ConcurrencyTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task SqliteDialect_ShouldRequireDeploymentOwnedSingleApplicantCoordination()
    {
        await using var migrator = CreateMigrator();

        migrator.CoordinationMode.Should().Be(MigrationCoordinationMode.SingleApplicantRequired);
    }
}
