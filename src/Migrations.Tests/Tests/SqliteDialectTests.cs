using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>Direct SQLite dialect checks for the parts the runner does not exercise (the CLI calls EnsureDatabaseExists).</summary>
public sealed class SqliteDialectTests
{
    [Fact]
    public async Task EnsureDatabaseExists_CreatesParentDirectory_AndReportsFirstCreate()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wow2-mig-sqlite-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "nested", "app.db");
        try
        {
            var dialect = new SqliteMigrationDialect();

            // First ensure: file absent → creates the parent dir and reports a create (the file itself is created
            // lazily by SQLite on first open, so it is still absent here).
            var created = await dialect.EnsureDatabaseExistsAsync($"Data Source={path}", CancellationToken.None);
            created.Should().BeTrue();
            Directory.Exists(Path.GetDirectoryName(path)).Should().BeTrue();
            File.Exists(path).Should().BeFalse();

            // With the file present, ensure reports no create.
            await File.WriteAllTextAsync(path, string.Empty);
            (await dialect.EnsureDatabaseExistsAsync($"Data Source={path}", CancellationToken.None)).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureDatabaseExists_ForInMemory_ReportsNoCreate()
    {
        var dialect = new SqliteMigrationDialect();
        (await dialect.EnsureDatabaseExistsAsync("Data Source=:memory:", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public void QualifyHistoryTable_IgnoresSchema_AndQuotesTableOnly()
    {
        var dialect = new SqliteMigrationDialect();
        dialect.QualifyHistoryTable("public", "migration_history").Should().Be("\"migration_history\"");
    }
}
