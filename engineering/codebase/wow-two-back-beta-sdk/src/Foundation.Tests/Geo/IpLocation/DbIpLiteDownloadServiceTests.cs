using System.IO.Compression;
using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.BackgroundServices;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Services;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Geo.IpLocation;

/// <summary>The DB-IP Lite refresh falls back a month, skips a current copy and never installs a bad file.</summary>
public sealed class DbIpLiteDownloadServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wow2-dbip-lite", Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
    private readonly Dictionary<string, Func<HttpResponseMessage>> _releases = new(StringComparer.Ordinal);
    private readonly List<string> _requests = [];

    [Fact]
    public async Task Refresh_FallsBackToThePreviousMonth_ThenAdoptsTheCurrentRelease()
    {
        _releases["2026-08"] = () => Gzip(ValidDatabase("AU"));
        var service = CreateService();

        (await service.RefreshAsync(CancellationToken.None)).Should().BeTrue();
        (await service.RefreshAsync(CancellationToken.None)).Should().BeFalse("August is still the newest published release");

        _releases["2026-09"] = () => Gzip(ValidDatabase("NZ"));
        (await service.RefreshAsync(CancellationToken.None)).Should().BeTrue();
        (await service.RefreshAsync(CancellationToken.None)).Should().BeFalse();

        _requests.Should().Equal("2026-09", "2026-08", "2026-09", "2026-09");
        File.Exists(service.DatabasePath).Should().BeTrue();
        Directory.GetFiles(_directory, "*.download").Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_KeepsTheInstalledDatabase_WhenTheNewReleaseIsInvalid()
    {
        _releases["2026-09"] = () => Gzip(ValidDatabase("AU"));
        var service = CreateService();
        (await service.RefreshAsync(CancellationToken.None)).Should().BeTrue();
        byte[] installed = await File.ReadAllBytesAsync(service.DatabasePath);

        _clock.Advance(TimeSpan.FromDays(10));
        _releases["2026-10"] = () => Gzip([9, 9, 9, 9]);

        (await service.RefreshAsync(CancellationToken.None)).Should().BeFalse();
        (await File.ReadAllBytesAsync(service.DatabasePath)).Should().Equal(installed);
        Directory.GetFiles(_directory, "*.download").Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_RejectsADatabaseAboveTheSizeLimit()
    {
        _releases["2026-09"] = () => Gzip(ValidDatabase("AU"));
        var service = CreateService(maxBytes: 16);

        (await service.RefreshAsync(CancellationToken.None)).Should().BeFalse();
        File.Exists(service.DatabasePath).Should().BeFalse();
    }

    [Fact]
    public async Task BackgroundRefresh_RequestsNothing_WhenDownloadIsDisabled()
    {
        _releases["2026-09"] = () => Gzip(ValidDatabase("AU"));
        var options = new DbIpLiteOptions
        {
            DatabaseDirectory = _directory,
            DownloadUrlFormat = "https://releases.test/dbip-{0}-lite-{1:yyyy-MM}.mmdb.gz",
            EnableDownload = false,
        };
        var downloads = new DbIpLiteDownloadService(
            new SingleClientFactory(new ReleaseHandler(this)), options, _clock, NullLogger<DbIpLiteDownloadService>.Instance);
        using var worker = new DbIpLiteDownloadBackgroundService(
            downloads, options, _clock, NullLogger<DbIpLiteDownloadBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        _requests.Should().BeEmpty();
        File.Exists(downloads.DatabasePath).Should().BeFalse();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temp directory must not fail a test.
        }
    }

    private DbIpLiteDownloadService CreateService(long maxBytes = 1024 * 1024) => new(
        new SingleClientFactory(new ReleaseHandler(this)),
        new DbIpLiteOptions
        {
            DatabaseDirectory = _directory,
            DownloadUrlFormat = "https://releases.test/dbip-{0}-lite-{1:yyyy-MM}.mmdb.gz",
            MaxDatabaseBytes = maxBytes,
        },
        _clock,
        NullLogger<DbIpLiteDownloadService>.Instance);

    private byte[] ValidDatabase(string countryCode)
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, $"source-{Guid.NewGuid():N}.bin");
        MmdbTestDatabaseWriter.Write(path, [("1.2.3.0/24", IpLocationTestData.Country(countryCode, countryCode, "OC"))]);
        byte[] bytes = File.ReadAllBytes(path);
        File.Delete(path);
        return bytes;
    }

    private static HttpResponseMessage Gzip(byte[] payload)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(payload);
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(buffer.ToArray()) };
    }

    private sealed class ReleaseHandler(DbIpLiteDownloadServiceTests owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string month = request.RequestUri!.AbsolutePath[^"2026-09.mmdb.gz".Length..^".mmdb.gz".Length];
            owner._requests.Add(month);
            return Task.FromResult(owner._releases.TryGetValue(month, out var release)
                ? release()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
