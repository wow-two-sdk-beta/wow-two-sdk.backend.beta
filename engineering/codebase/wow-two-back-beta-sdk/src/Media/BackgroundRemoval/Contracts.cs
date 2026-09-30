namespace WoW.Two.Sdk.Backend.Beta.Media.BackgroundRemoval;

/// <summary>Optional background segmentation without persistence or publication side effects.</summary>
public interface IBackgroundRemovalService
{
    /// <summary>Checks that the configured worker has loaded its model.</summary>
    Task<BackgroundRemovalStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>Removes the background from bounded encoded raster bytes.</summary>
    Task<BackgroundRemovalResult> RemoveAsync(byte[] source, CancellationToken cancellationToken = default);
}
/// <summary>Actual worker readiness, without addresses or credentials.</summary>
public sealed record BackgroundRemovalStatus(bool Available, string? Model = null);
/// <summary>Validated PNG bytes and the worker model that produced them.</summary>
public sealed record BackgroundRemovalResult(byte[] Content, int Width, int Height, string Model);
/// <summary>Safe failure classification, excluding worker responses and credentials.</summary>
public sealed class BackgroundRemovalException(string code, string message) : Exception(message)
{
    /// <summary>Gets the failure category.</summary>
    public string Code { get; } = code;
}
/// <summary>Explicit host-only private worker configuration; absent URL/key disables processing.</summary>
public sealed class BackgroundRemovalOptions
{
    /// <summary>Gets or sets an HTTPS origin, or literal loopback HTTP origin.</summary>
    public string? BaseUrl { get; set; }
    /// <summary>Gets or sets the private worker key. Never expose it to browser clients.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Gets or sets the encoded input/output limit.</summary>
    public int MaximumBytes { get; set; } = 20 * 1024 * 1024;
    /// <summary>Gets or sets the maximum decoded pixel count.</summary>
    public long MaximumPixels { get; set; } = 20_000_000;
    /// <summary>Gets or sets the maximum image side.</summary>
    public int MaximumDimension { get; set; } = 8192;
    /// <summary>Gets or sets the processing timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(90);
}
