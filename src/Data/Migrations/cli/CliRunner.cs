using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Cli;

/// <summary>Provides the migrator command bodies — resolves connection and source, wires the runner via DI, and prints results.</summary>
/// <remarks>
/// Build flow per command:
///   1. Resolve the connection string + migrations dir (flag → env → default)
///   2. Register the Npgsql data source, the <see cref="DataSourceConnectionFactory"/> connection seam, and the filesystem migrator via <c>AddDatabaseBespokeMigrations</c>
///   3. Resolve <see cref="IMigrationRunnerService"/> from the provider and run the operation
/// </remarks>
internal static partial class CliRunner
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    /// <summary>Resolves the connection string from the flag, the environment, then the localhost default.</summary>
    /// <param name="connectionFlag">The value of the global <c>--connection</c> option, or null when absent.</param>
    public static string ResolveConnection(string? connectionFlag) =>
        connectionFlag
        ?? Environment.GetEnvironmentVariable("WOW_MIGRATE_DB_CONNECTION")
        ?? DefaultConnection;

    /// <summary>Resolves the migrations directory from the flag, the environment, then the auto-discovered default.</summary>
    /// <param name="sqlDirFlag">The value of the global <c>--sql-dir</c> option, or null when absent.</param>
    public static string ResolveSqlDir(string? sqlDirFlag) =>
        sqlDirFlag
        ?? Environment.GetEnvironmentVariable("WOW_MIGRATE_SQL_DIR")
        ?? MigrationsPathResolver.Resolve();

    /// <summary>Prints the applied, pending, drifted, and orphaned migrations for the resolved database.</summary>
    /// <param name="connection">The resolved connection string.</param>
    /// <param name="sqlDir">The resolved migrations directory.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    public static async Task<int> StatusAsync(string? connection, string? sqlDir, CancellationToken ct)
    {
        await using var provider = BuildProvider(connection, sqlDir);
        var status = await provider.GetRequiredService<IMigrationRunnerService>().GetStatusAsync(ct);

        Console.WriteLine($"Applied ({status.Applied.Count}):");
        foreach (var row in status.Applied)
            Console.WriteLine($"  ✓ {row.Ordinal:D3}-{row.Name}   [{row.AppliedBy} @ {row.AppliedAt:u}, {row.ExecutionMs}ms]");

        Console.WriteLine($"Pending ({status.Pending.Count}):");
        foreach (var migration in status.Pending)
            Console.WriteLine($"  • {migration.Label}");

        if (status.Drifted.Count > 0)
        {
            Console.WriteLine($"Drift ({status.Drifted.Count}):");
            foreach (var migration in status.Drifted)
                Console.WriteLine($"  ! {migration.Label}");
        }

        if (status.Orphaned.Count > 0)
            Console.WriteLine($"Orphaned (in DB, not in source): {string.Join(", ", status.Orphaned.Select(o => o.ToString("D3")))}");

        return 0;
    }

    /// <summary>Ensures the database exists, then applies every pending migration.</summary>
    /// <param name="connection">The resolved connection string.</param>
    /// <param name="sqlDir">The resolved migrations directory.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    public static async Task<int> ApplyAsync(string? connection, string? sqlDir, CancellationToken ct)
    {
        await using var provider = BuildProvider(connection, sqlDir);

        // Create the target database first — a fresh box may have no target database yet.
        await provider.GetRequiredService<IMigrationDialect>().EnsureDatabaseExistsAsync(ResolveConnection(connection), ct);
        var applied = await provider.GetRequiredService<IMigrationRunnerService>().ApplyPendingAsync("cli", ct);

        Console.WriteLine(applied.Count == 0
            ? "✓ Up to date — nothing to apply."
            : $"✓ Applied {applied.Count}: {string.Join(", ", applied)}");
        return 0;
    }

    /// <summary>Rolls back the latest migration, or every migration above the given ordinal (dev only, target-guarded).</summary>
    /// <param name="connection">The resolved connection string.</param>
    /// <param name="sqlDir">The resolved migrations directory.</param>
    /// <param name="targetOrdinal">The ordinal to roll back to, or null to roll back only the latest.</param>
    /// <param name="confirmDb">The operator's claimed target database name, from <c>--i-understand-this-is</c>.</param>
    /// <param name="force">Whether to skip the interactive confirmation prompt.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    public static async Task<int> RollbackAsync(string? connection, string? sqlDir, int? targetOrdinal, string? confirmDb, bool force, CancellationToken ct)
    {
        if (!ConfirmDestructiveTarget(connection, confirmDb, force))
            return 2;

        await using var provider = BuildProvider(connection, sqlDir, allowRollback: true);
        var rolledBack = await provider.GetRequiredService<IMigrationRunnerService>().RollbackAsync(targetOrdinal, ct);

        Console.WriteLine(rolledBack.Count == 0
            ? "Nothing to roll back."
            : $"✓ Rolled back {rolledBack.Count}: {string.Join(", ", rolledBack)}");
        return 0;
    }

    /// <summary>Reports checksum drift (exit 1 on any), or re-records edited checksums when repair is requested (repair is target-guarded).</summary>
    /// <param name="connection">The resolved connection string.</param>
    /// <param name="sqlDir">The resolved migrations directory.</param>
    /// <param name="repair">Whether to re-record drifted checksums instead of failing on them.</param>
    /// <param name="confirmDb">The operator's claimed target database name, from <c>--i-understand-this-is</c> (required only with <paramref name="repair"/>).</param>
    /// <param name="force">Whether to skip the interactive confirmation prompt when repairing.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    public static async Task<int> VerifyAsync(string? connection, string? sqlDir, bool repair, string? confirmDb, bool force, CancellationToken ct)
    {
        // Plain verify is read-only; only --repair mutates migration_history, so guard only that path.
        if (repair && !ConfirmDestructiveTarget(connection, confirmDb, force))
            return 2;

        await using var provider = BuildProvider(connection, sqlDir, allowRollback: repair);
        var runner = provider.GetRequiredService<IMigrationRunnerService>();

        if (repair)
        {
            var repaired = await runner.RepairAsync(ct);
            Console.WriteLine(repaired.Count == 0
                ? "✓ No drift to repair."
                : $"✓ Repaired checksums: {string.Join(", ", repaired)}");
            return 0;
        }

        var status = await runner.GetStatusAsync(ct);
        if (status.Drifted.Count == 0)
        {
            Console.WriteLine("✓ No drift — every applied migration matches its source.");
            return 0;
        }

        await Console.Error.WriteLineAsync(
            $"✗ Drift in {status.Drifted.Count}: {string.Join(", ", status.Drifted.Select(m => m.Label))}. " +
            "Revert the edits, or run 'verify --repair' to accept them.");
        return 1;
    }

    /// <summary>Scaffolds a new <c>Dev/&lt;name&gt;.sql</c> draft under the migrations directory.</summary>
    /// <param name="sqlDir">The resolved migrations directory.</param>
    /// <param name="name">The draft name, slugified before use.</param>
    public static int NewMigration(string? sqlDir, string name)
    {
        var slug = Slugify(name);
        var devDir = Path.Combine(ResolveSqlDir(sqlDir), MigrationConventions.DevFolderName);
        Directory.CreateDirectory(devDir);

        var path = Path.Combine(devDir, $"{slug}.sql");
        if (File.Exists(path))
        {
            Console.Error.WriteLine($"✗ {path} already exists.");
            return 1;
        }

        File.WriteAllText(path,
            $"-- Dev migration: {slug}\n" +
            "-- Iterate freely. Promote to a numbered Apply/Rollback pair with: wow-migrate promote\n" +
            $"-- Put '{MigrationConventions.NoTransactionDirective}' as the FIRST line if this must run outside a transaction\n" +
            "--   (such files re-run on a mid-apply crash, so make them idempotent: IF NOT EXISTS, guarded DO blocks).\n\n");

        Console.WriteLine($"✓ Created {path}");
        return 0;
    }

    /// <summary>Promotes every <c>Dev/*.sql</c> draft into a numbered <c>NNN-name/{Apply,Rollback}.sql</c> migration.</summary>
    /// <param name="sqlDir">The resolved migrations directory.</param>
    public static int PromoteDev(string? sqlDir)
    {
        var root = ResolveSqlDir(sqlDir);
        var devDir = Path.Combine(root, MigrationConventions.DevFolderName);
        if (!Directory.Exists(devDir))
        {
            Console.WriteLine("No Dev/ folder — nothing to promote.");
            return 0;
        }

        var devFiles = Directory.GetFiles(devDir, "*.sql").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (devFiles.Count == 0)
        {
            Console.WriteLine("No Dev/*.sql — nothing to promote.");
            return 0;
        }

        var next = NextOrdinal(root);
        foreach (var file in devFiles)
        {
            var name = Slugify(Path.GetFileNameWithoutExtension(file));
            var folder = Path.Combine(root, $"{next:D3}-{name}");
            Directory.CreateDirectory(folder);

            File.Copy(file, Path.Combine(folder, MigrationConventions.ApplyFileName));
            File.WriteAllText(Path.Combine(folder, MigrationConventions.RollbackFileName),
                $"-- Rollback for {next:D3}-{name}. Write the inverse of Apply.sql (dev/test only).\n");
            File.Delete(file);

            Console.WriteLine($"✓ Promoted Dev/{Path.GetFileName(file)} → {next:D3}-{name}/");
            next++;
        }

        return 0;
    }

    /// <summary>Builds the DI provider that registers the data source, connection factory, and filesystem migrator.</summary>
    /// <param name="connection">The global connection flag, resolved against env and default.</param>
    /// <param name="sqlDir">The global sql-dir flag, resolved against env and default.</param>
    /// <param name="allowRollback">Whether to enable the engine's destructive ops (rollback / repair) — only the guarded destructive verbs pass true.</param>
    private static ServiceProvider BuildProvider(string? connection, string? sqlDir, bool allowRollback = false)
    {
        var dataSource = new NpgsqlDataSourceBuilder(ResolveConnection(connection)).Build();

        return new ServiceCollection()
            .AddSingleton<DbDataSource>(dataSource)
            .AddDataSourceConnectionFactory()
            .AddLogging()
            .AddDatabaseBespokeMigrations(ResolveSqlDir(sqlDir), options => options.AllowRollback = allowRollback)
            .BuildServiceProvider();
    }

    /// <summary>Confirms a destructive op against the resolved target database — the operator must name it, then confirm unless forced.</summary>
    /// <param name="connection">The global connection flag, resolved to derive the target database name.</param>
    /// <param name="confirmDb">The operator's claimed database name, from <c>--i-understand-this-is</c>.</param>
    /// <param name="force">Whether to skip the interactive <c>[y/N]</c> prompt.</param>
    private static bool ConfirmDestructiveTarget(string? connection, string? confirmDb, bool force)
    {
        var target = new NpgsqlConnectionStringBuilder(ResolveConnection(connection)).Database ?? "(unknown)";

        if (!string.Equals(confirmDb, target, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"✗ Destructive op refused. Re-run with --i-understand-this-is {target} to confirm the target database.");
            return false;
        }

        if (force)
            return true;

        Console.Write($"This will modify database '{target}'. Continue? [y/N] ");
        if (Console.ReadLine()?.Trim() is "y" or "Y" or "yes" or "YES")
            return true;

        Console.Error.WriteLine("✗ Aborted.");
        return false;
    }

    /// <summary>Computes the next ordinal — one past the highest <c>NNN-</c> prefix already on disk.</summary>
    /// <param name="root">The migrations directory to scan.</param>
    private static int NextOrdinal(string root)
    {
        var max = 0;
        foreach (var dir in Directory.GetDirectories(root))
        {
            var match = OrdinalPrefix().Match(Path.GetFileName(dir));
            if (match.Success)
                max = Math.Max(max, int.Parse(match.Groups[1].Value));
        }

        return max + 1;
    }

    /// <summary>Lowercases and hyphenates a name into a filesystem-safe slug.</summary>
    /// <param name="value">The raw name to slugify.</param>
    private static string Slugify(string value) =>
        new string(value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    /// <summary>Matches the leading three-digit ordinal of a numbered migration folder.</summary>
    [GeneratedRegex(@"^(\d{3})-")]
    private static partial Regex OrdinalPrefix();
}
