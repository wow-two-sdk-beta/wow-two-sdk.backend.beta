using System.Collections.Concurrent;
using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using WoW.Two.Sdk.Backend.Beta.Observability.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Logging;

/// <summary>Verifies durable pre-host failure capture, exception preservation and final-logger handoff.</summary>
[Collection(StartupFailureReporterCollection.Name)]
public sealed class StartupFailureReporterTests
{
    [Theory]
    [InlineData("creation", "creation probe failed")]
    [InlineData("validation", "probe options invalid")]
    public async Task ChildProcess_ShouldPersistStartupFailureAndExitNonzero(string mode, string expected)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "wow2-startup-tests", Guid.NewGuid().ToString("N"), "startup.log");
        var result = await RunProbeAsync(mode, logPath);

        result.ExitCode.Should().NotBe(0, result.StandardOutput + result.StandardError);
        File.Exists(logPath).Should().BeTrue();
        (await File.ReadAllTextAsync(logPath)).Should().Contain(expected);
    }

    [Fact]
    public async Task NormalRun_ShouldHandOffWithoutDuplicatingApplicationEvents()
    {
        var startupLog = Path.Combine(Path.GetTempPath(), "wow2-startup-tests", Guid.NewGuid().ToString("N"), "startup.log");
        var sink = new CollectingSink();

        await StartupFailureReporter.RunAsync(_ =>
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
            Log.Information("application probe");
            return Task.CompletedTask;
        }, startupLog);

        sink.Events.Should().ContainSingle(logEvent => logEvent.RenderMessage() == "application probe");
        if (File.Exists(startupLog))
            (await File.ReadAllTextAsync(startupLog)).Should().NotContain("application probe");
    }

    [Fact]
    public async Task StartupFailureChildProcessProbe()
    {
        var mode = Environment.GetEnvironmentVariable("WOW2_STARTUP_FAILURE_PROBE");
        if (string.IsNullOrEmpty(mode))
            return;

        var path = Environment.GetEnvironmentVariable("WOW2_STARTUP_FAILURE_LOG")!;
        await StartupFailureReporter.RunAsync(async cancellationToken =>
        {
            if (mode == "creation")
                throw new InvalidOperationException("creation probe failed");

            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services => services
                    .AddOptions<ProbeOptions>()
                    .Validate(options => !string.IsNullOrWhiteSpace(options.Name), "probe options invalid")
                    .ValidateOnStart())
                .Build();
            await host.StartAsync(cancellationToken);
        }, path);
    }

    private static async Task<ProcessResult> RunProbeAsync(string mode, string logPath)
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var project = Path.Combine(output.Parent!.Parent!.Parent!.FullName,
            "WoW.Two.Sdk.Backend.Beta.Web.Tests.csproj");
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("-m:1");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add(
            "FullyQualifiedName~StartupFailureReporterTests.StartupFailureChildProcessProbe");
        startInfo.Environment["WOW2_STARTUP_FAILURE_PROBE"] = mode;
        startInfo.Environment["WOW2_STARTUP_FAILURE_LOG"] = logPath;

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    private sealed class ProbeOptions
    {
        public string Name { get; set; } = "";
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public ConcurrentBag<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

/// <summary>Serializes tests that replace Serilog's process-global logger.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StartupFailureReporterCollection
{
    /// <summary>The xUnit collection name.</summary>
    public const string Name = "Startup failure reporter";
}
