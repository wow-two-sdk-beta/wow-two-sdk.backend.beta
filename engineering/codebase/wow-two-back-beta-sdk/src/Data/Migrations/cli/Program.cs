using System.CommandLine;
using Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Cli;

// Run the web-free migration CLI assembled by CliCommands and CliRunner.

DefaultTypeMap.MatchNamesWithUnderscores = true; // Dapper snake_case → PascalCase for migration_history reads

// Disable the built-in handler so action exceptions surface as a clean "✗ message" below, not a stack trace.
var configuration = new InvocationConfiguration { EnableDefaultExceptionHandler = false };

try
{
    return await new CliCommandBuilder().Build().Parse(args).InvokeAsync(configuration);
}
// Exit codes: 0 success · 1 validation (drift, bad config / missing file) · 2 execution (DB / apply failure, guard tripped).
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"✗ {ex.Message}");
    return ex switch
    {
        DirectoryNotFoundException or FileNotFoundException => 1,
        _ => 2,
    };
}
