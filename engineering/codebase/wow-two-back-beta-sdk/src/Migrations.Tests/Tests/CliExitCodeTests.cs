using System.Diagnostics;
using AwesomeAssertions;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>Verifies the migration CLI's process-level exit-code contract.</summary>
public sealed class CliExitCodeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "wow2-migration-cli-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task New_ShouldExitZero_WhenInputIsValid()
    {
        Directory.CreateDirectory(_root);
        var result = await RunAsync("new", "sample", "--sql-dir", _root);

        result.ExitCode.Should().Be(0, result.StandardError);
        File.Exists(Path.Combine(_root, "Dev", "sample.sql")).Should().BeTrue();
    }

    [Fact]
    public async Task New_ShouldExitOne_WhenRequiredNameIsMissing()
    {
        Directory.CreateDirectory(_root);
        var result = await RunAsync("new", "--sql-dir", _root);

        result.ExitCode.Should().Be(1, result.StandardError);
    }

    [Fact]
    public async Task Rollback_ShouldExitTwo_WhenTargetGuardRefuses()
    {
        Directory.CreateDirectory(_root);
        var result = await RunAsync(
            "rollback",
            "--connection", "Host=localhost;Database=guarded;Username=test;Password=test",
            "--sql-dir", _root,
            "--i-understand-this-is", "wrong",
            "--force");

        result.ExitCode.Should().Be(2, result.StandardError);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static async Task<ProcessResult> RunAsync(params string[] arguments)
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var targetFramework = output.Name;
        var configuration = output.Parent!.Name;
        var sourceRoot = output.Parent.Parent!.Parent!.Parent!.FullName;
        var cli = Path.Combine(
            sourceRoot, "Data", "Migrations", "cli", "bin", configuration, targetFramework, "wow-migrate.dll");
        File.Exists(cli).Should().BeTrue($"the CLI project should build before tests: {cli}");

        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(cli);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
