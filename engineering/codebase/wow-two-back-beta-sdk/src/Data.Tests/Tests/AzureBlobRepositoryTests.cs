using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Azurite;
using WoW.Two.Sdk.Backend.Beta.Storage.Azure;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>The Azure blob repository against Azurite: container creation, save, read, info, list by prefix, delete.</summary>
[SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Teardown runs in IAsyncLifetime.DisposeAsync, which xUnit invokes.")]
public sealed class AzureBlobRepositoryTests : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite = new AzuriteBuilder().WithImage("mcr.microsoft.com/azure-storage/azurite:latest").WithInMemoryPersistence().Build();
    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        await _azurite.StartAsync();
        var services = new ServiceCollection();
        services.AddAzureBlobStorage(o =>
        {
            o.ContainerName = "blobs";
            o.ConnectionString = _azurite.GetConnectionString();
            o.KeyPrefix = "app-a";
            o.CreateContainerIfMissing = true;
        });
        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
        await _azurite.DisposeAsync();
    }

    [Fact]
    public async Task SasUrls_ShouldUploadAndDownloadWithoutTheApi()
    {
        var blobs = _provider!.GetRequiredService<IBlobRepository>();
        await blobs.SaveAsync("seed.txt", new MemoryStream(Encoding.UTF8.GetBytes("seed")));
        var urls = _provider!.GetRequiredService<IBlobUrlIssuer>();
        var upload = await urls.IssueWriteUrlAsync("direct/upload.txt", TimeSpan.FromMinutes(5), "text/plain");
        Assert.Equal("BlockBlob", upload.Headers["x-ms-blob-type"]);
        Assert.Contains("sp=cw", upload.Url.Query, StringComparison.Ordinal);

        using var http = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, upload.Url) { Content = new StringContent("straight to the container", Encoding.UTF8) };
        foreach (var (name, value) in upload.Headers.Where(header => header.Key != "Content-Type"))
            put.Headers.TryAddWithoutValidation(name, value);
        put.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        Assert.True((await http.SendAsync(put)).IsSuccessStatusCode);

        var download = await urls.IssueReadUrlAsync("direct/upload.txt", TimeSpan.FromMinutes(5), "notes.txt");
        using var response = await http.GetAsync(download.Url);
        Assert.Equal("straight to the container", await response.Content.ReadAsStringAsync());
        Assert.Equal("notes.txt", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var forged = new Uri(download.Url.ToString().Replace("sp=r", "sp=rw", StringComparison.Ordinal));
        Assert.False((await http.GetAsync(forged)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task RoundTripsBlobsUnderTheKeyPrefix()
    {
        var blobs = _provider!.GetRequiredService<IBlobRepository>();

        await blobs.SaveAsync("avatars/u1.png", new MemoryStream(Encoding.UTF8.GetBytes("png-bytes")), "image/png");
        await blobs.SaveAsync("avatars/u2.png", new MemoryStream(Encoding.UTF8.GetBytes("second")), "image/png");
        await blobs.SaveAsync("docs/readme.txt", new MemoryStream(Encoding.UTF8.GetBytes("hello")));

        await using (var read = await blobs.OpenReadAsync("avatars/u1.png"))
        {
            Assert.NotNull(read);
            Assert.Equal("png-bytes", await new StreamReader(read).ReadToEndAsync());
        }

        var info = await blobs.GetInfoAsync("avatars/u2.png");
        Assert.Equal(new { Path = "avatars/u2.png", SizeBytes = 6L, ContentType = (string?)"image/png" }, new { info!.Path, info.SizeBytes, info.ContentType });

        var avatars = new List<string>();
        await foreach (var blob in blobs.ListAsync("avatars/"))
            avatars.Add(blob.Path);
        Assert.Equal(["avatars/u1.png", "avatars/u2.png"], avatars);

        Assert.True(await blobs.DeleteAsync("avatars/u1.png"));
        Assert.False(await blobs.DeleteAsync("avatars/u1.png"));
        Assert.Null(await blobs.OpenReadAsync("avatars/u1.png"));
        Assert.Null(await blobs.GetInfoAsync("avatars/u1.png"));
    }
}
