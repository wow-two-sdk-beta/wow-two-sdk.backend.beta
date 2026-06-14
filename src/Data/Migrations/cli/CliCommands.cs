using System.CommandLine;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Cli;

/// <summary>Builds the <c>wow-migrate</c> command tree — the global options and the status, apply, rollback, verify, new, and promote verbs.</summary>
/// <remarks>
/// Tree shape:
///   root (--connection, --sql-dir recursive)
///     ├─ status · apply · rollback (--to) · verify (--repair) · new &lt;name&gt; · promote
/// Each verb's action resolves the runner via DI and delegates to <see cref="CliRunner"/>.
/// </remarks>
internal static class CliCommands
{
    /// <summary>Builds the root command with the global options applied recursively to every subcommand.</summary>
    public static RootCommand Build()
    {
        var connectionOption = new Option<string?>("--connection")
        {
            Description = "Postgres connection (else WOW_MIGRATE_DB_CONNECTION, else localhost/postgres).",
            Recursive = true,
        };

        var sqlDirOption = new Option<string?>("--sql-dir")
        {
            Description = "Migrations dir (else WOW_MIGRATE_SQL_DIR, else auto-discovered).",
            Recursive = true,
        };

        var root = new RootCommand("wow-migrate — web-free SQL migrator CLI.");
        root.Options.Add(connectionOption);
        root.Options.Add(sqlDirOption);

        root.Subcommands.Add(BuildStatus(connectionOption, sqlDirOption));
        root.Subcommands.Add(BuildApply(connectionOption, sqlDirOption));
        root.Subcommands.Add(BuildRollback(connectionOption, sqlDirOption));
        root.Subcommands.Add(BuildVerify(connectionOption, sqlDirOption));
        root.Subcommands.Add(BuildNew(sqlDirOption));
        root.Subcommands.Add(BuildPromote(sqlDirOption));

        return root;
    }

    /// <summary>Builds the <c>status</c> verb — show applied, pending, drift, and orphaned migrations.</summary>
    /// <param name="connection">The global connection option to read in the action.</param>
    /// <param name="sqlDir">The global sql-dir option to read in the action.</param>
    private static Command BuildStatus(Option<string?> connection, Option<string?> sqlDir)
    {
        var command = new Command("status", "Show applied / pending / drift / orphaned.");
        command.SetAction((parseResult, ct) =>
            CliRunner.StatusAsync(parseResult.GetValue(connection), parseResult.GetValue(sqlDir), ct));
        return command;
    }

    /// <summary>Builds the <c>apply</c> verb — ensure the database exists, then apply pending migrations.</summary>
    /// <param name="connection">The global connection option to read in the action.</param>
    /// <param name="sqlDir">The global sql-dir option to read in the action.</param>
    private static Command BuildApply(Option<string?> connection, Option<string?> sqlDir)
    {
        var command = new Command("apply", "Ensure the DB exists, then apply pending migrations.");
        command.SetAction((parseResult, ct) =>
            CliRunner.ApplyAsync(parseResult.GetValue(connection), parseResult.GetValue(sqlDir), ct));
        return command;
    }

    /// <summary>Builds the <c>rollback</c> verb — roll back the latest migration, or down to <c>--to N</c> (dev only).</summary>
    /// <param name="connection">The global connection option to read in the action.</param>
    /// <param name="sqlDir">The global sql-dir option to read in the action.</param>
    private static Command BuildRollback(Option<string?> connection, Option<string?> sqlDir)
    {
        var toOption = new Option<int?>("--to")
        {
            Description = "Roll back down to this ordinal (else just the latest).",
        };

        var confirmOption = new Option<string?>("--i-understand-this-is")
        {
            Description = "Confirm the target database name (must match the connection's database).",
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Skip the interactive [y/N] confirmation prompt.",
        };

        var command = new Command("rollback", "Roll back the latest migration (or down to ordinal N). Dev only.")
        {
            toOption,
            confirmOption,
            forceOption,
        };
        command.SetAction((parseResult, ct) =>
            CliRunner.RollbackAsync(
                parseResult.GetValue(connection), parseResult.GetValue(sqlDir), parseResult.GetValue(toOption),
                parseResult.GetValue(confirmOption), parseResult.GetValue(forceOption), ct));
        return command;
    }

    /// <summary>Builds the <c>verify</c> verb — fail on checksum drift, or re-record it with <c>--repair</c>.</summary>
    /// <param name="connection">The global connection option to read in the action.</param>
    /// <param name="sqlDir">The global sql-dir option to read in the action.</param>
    private static Command BuildVerify(Option<string?> connection, Option<string?> sqlDir)
    {
        var repairOption = new Option<bool>("--repair")
        {
            Description = "Re-record edited checksums instead of failing on drift.",
        };

        var confirmOption = new Option<string?>("--i-understand-this-is")
        {
            Description = "Confirm the target database name (required with --repair).",
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Skip the interactive [y/N] confirmation prompt when repairing.",
        };

        var command = new Command("verify", "Exit 1 on checksum drift; --repair re-records edited checksums.")
        {
            repairOption,
            confirmOption,
            forceOption,
        };
        command.SetAction((parseResult, ct) =>
            CliRunner.VerifyAsync(
                parseResult.GetValue(connection), parseResult.GetValue(sqlDir), parseResult.GetValue(repairOption),
                parseResult.GetValue(confirmOption), parseResult.GetValue(forceOption), ct));
        return command;
    }

    /// <summary>Builds the <c>new</c> verb — scaffold a <c>Dev/&lt;name&gt;.sql</c> draft.</summary>
    /// <param name="sqlDir">The global sql-dir option to read in the action.</param>
    private static Command BuildNew(Option<string?> sqlDir)
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "The draft migration name (slugified).",
        };

        var command = new Command("new", "Scaffold Dev/<name>.sql.")
        {
            nameArgument,
        };
        command.SetAction((parseResult, _) =>
            Task.FromResult(CliRunner.NewMigration(parseResult.GetValue(sqlDir), parseResult.GetValue(nameArgument)!)));
        return command;
    }

    /// <summary>Builds the <c>promote</c> verb — promote <c>Dev/*.sql</c> drafts into numbered migrations.</summary>
    /// <param name="sqlDir">The global sql-dir option to read in the action.</param>
    private static Command BuildPromote(Option<string?> sqlDir)
    {
        var command = new Command("promote", "Promote Dev/*.sql → numbered NNN-name/{Apply,Rollback}.sql.");
        command.SetAction((parseResult, _) =>
            Task.FromResult(CliRunner.PromoteDev(parseResult.GetValue(sqlDir))));
        return command;
    }
}
