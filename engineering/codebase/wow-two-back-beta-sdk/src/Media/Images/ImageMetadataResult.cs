namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents what the camera recorded: when, with what, how and where; each field is null when absent.</summary>
public sealed record ImageMetadataResult
{
    /// <summary>Gets the moment the photo was taken, in the camera's wall-clock time.</summary>
    public DateTime? TakenAt { get; init; }

    /// <summary>Gets the camera's UTC offset for <see cref="TakenAt"/>, when it recorded one.</summary>
    public TimeSpan? TakenAtOffset { get; init; }

    /// <summary>Gets the camera maker.</summary>
    public string? CameraMake { get; init; }

    /// <summary>Gets the camera model.</summary>
    public string? CameraModel { get; init; }

    /// <summary>Gets the lens model.</summary>
    public string? LensModel { get; init; }

    /// <summary>Gets the software that last wrote the file.</summary>
    public string? Software { get; init; }

    /// <summary>Gets the EXIF orientation, 1–8; 1 when absent.</summary>
    public int Orientation { get; init; } = 1;

    /// <summary>Gets the exposure time as a fraction of a second, such as <c>1/250</c>.</summary>
    public string? ExposureTime { get; init; }

    /// <summary>Gets the aperture f-number, such as 2.8.</summary>
    public double? FNumber { get; init; }

    /// <summary>Gets the ISO sensitivity.</summary>
    public int? Iso { get; init; }

    /// <summary>Gets the focal length in millimetres.</summary>
    public double? FocalLength { get; init; }

    /// <summary>Gets the latitude in decimal degrees, south negative.</summary>
    public double? Latitude { get; init; }

    /// <summary>Gets the longitude in decimal degrees, west negative.</summary>
    public double? Longitude { get; init; }

    /// <summary>Gets the altitude in metres, below sea level negative.</summary>
    public double? Altitude { get; init; }

    /// <summary>Gets the moment taken with its offset, when the camera recorded both.</summary>
    public DateTimeOffset? TakenAtWithOffset => TakenAt is { } taken && TakenAtOffset is { } offset ? new DateTimeOffset(taken, offset) : null;

    /// <summary>Gets whether the file tells where it was taken — worth stripping before sharing.</summary>
    public bool HasLocation => Latitude is not null && Longitude is not null;
}
