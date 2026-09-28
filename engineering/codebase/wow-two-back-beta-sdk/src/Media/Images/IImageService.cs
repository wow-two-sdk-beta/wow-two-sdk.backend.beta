namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>
/// Defines behavior that reads, edits, combines and summarizes raster images — each call decodes once and encodes once.
/// Inputs over the configured limits raise <see cref="ImageRejectedException"/>.
/// </summary>
public interface IImageService
{
    /// <summary>Reads the format, upright size and orientation from the header, without decoding pixels.</summary>
    /// <param name="source">The encoded image.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<ImageProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default);

    /// <summary>Applies <paramref name="spec"/> — orient, crop, rotate, resize, overlays — and encodes the result.</summary>
    /// <param name="source">The encoded image.</param>
    /// <param name="spec">The edit.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<ImageResult> EditAsync(Stream source, ImageEditSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Arranges <paramref name="sources"/> into one image as <paramref name="spec"/> describes.</summary>
    /// <param name="sources">The encoded images, in reading order.</param>
    /// <param name="spec">The arrangement and output.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<ImageResult> CollageAsync(IReadOnlyList<Stream> sources, CollageSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Reads the EXIF block: when and where the photo was taken, and with what; fields are null when absent.</summary>
    /// <param name="source">The encoded image — JPEG, PNG or WebP.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<ImageMetadataResult> ReadMetadataAsync(Stream source, CancellationToken cancellationToken = default);

    /// <summary>Summarizes the image: BlurHash placeholder, average and dominant colors, perceptual hash.</summary>
    /// <param name="source">The encoded image.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<ImageAnalysisResult> AnalyzeAsync(Stream source, CancellationToken cancellationToken = default);
}
