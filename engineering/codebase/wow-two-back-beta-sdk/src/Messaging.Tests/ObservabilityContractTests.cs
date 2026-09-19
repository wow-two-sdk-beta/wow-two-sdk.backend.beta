using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using WoW.Two.Sdk.Backend.Beta.Messaging.Services;
using WoW.Two.Sdk.Backend.Beta.Observability.Metrics;
using WoW.Two.Sdk.Backend.Beta.Observability.Tracing;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Verifies SDK telemetry collection, bounded messaging dimensions and exporter isolation.</summary>
public sealed class ObservabilityContractTests
{
    [Fact]
    public void TracingHelper_ShouldCollectBrandPrefixedModuleSource()
    {
        var exporter = new CollectingActivityExporter();
        using var provider = new ServiceCollection()
            .AddOpenTelemetryTracing("contract-test")
            .ConfigureOpenTelemetryTracerProvider(builder =>
                builder.AddProcessor(new SimpleActivityExportProcessor(exporter)))
            .BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource("WoW.Two.ContractProbe");
        using (source.StartActivity("operation", ActivityKind.Internal))
        {
        }

        exporter.Exported.Should().ContainSingle(activity =>
            activity.Source.Name == "WoW.Two.ContractProbe" && activity.OperationName == "operation");
    }

    [Fact]
    public void MetricsHelper_ShouldCollectBrandPrefixedModuleMeter()
    {
        var exporter = new CollectingMetricExporter();
        using var provider = new ServiceCollection()
            .AddOpenTelemetryMetrics("contract-test")
            .ConfigureOpenTelemetryMeterProvider(builder => builder.AddReader(
                new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: 60_000)))
            .BuildServiceProvider();
        var meterProvider = provider.GetRequiredService<MeterProvider>();

        using var meter = new Meter("WoW.Two.ContractProbe");
        meter.CreateCounter<long>("contract_operations").Add(1);
        meterProvider.ForceFlush();

        exporter.Names.Should().Contain("contract_operations");
    }

    [Fact]
    public void MessagingMetrics_ShouldUseOnlyBoundedDimensions()
    {
        KeyValuePair<string, object?>[]? observedTags = null;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == MessagingMeterConstants.Name
                    && instrument.Name == MessagingMeterConstants.SentMessages)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => observedTags = tags.ToArray());
        listener.Start();

        using var provider = new ServiceCollection().AddMessagingMetrics().BuildServiceProvider();
        provider.GetRequiredService<IMessagingMetricsService>()
            .RecordPublished("orders", typeof(PingEvent));

        observedTags.Should().NotBeNull();
        observedTags!.Select(tag => tag.Key).Should().BeEquivalentTo(
            "messaging.destination.name", "messaging.message.type");
    }

    [Fact]
    public void ThrowingExporter_ShouldNotReplaceOperationResult()
    {
        using var provider = OpenTelemetry.Sdk.CreateTracerProviderBuilder()
            .AddSource("WoW.Two.ExporterProbe")
            .AddProcessor(new SimpleActivityExportProcessor(new ThrowingActivityExporter()))
            .Build();
        using var source = new ActivitySource("WoW.Two.ExporterProbe");

        var operation = () =>
        {
            using var activity = source.StartActivity("operation");
            return 42;
        };

        operation.Should().NotThrow().Which.Should().Be(42);
    }

    private sealed class CollectingActivityExporter : BaseExporter<Activity>
    {
        public ConcurrentBag<Activity> Exported { get; } = [];

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
                Exported.Add(activity);
            return ExportResult.Success;
        }
    }

    private sealed class CollectingMetricExporter : BaseExporter<Metric>
    {
        public ConcurrentBag<string> Names { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
                Names.Add(metric.Name);
            return ExportResult.Success;
        }
    }

    private sealed class ThrowingActivityExporter : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch) =>
            throw new InvalidOperationException("collector unavailable");
    }
}
