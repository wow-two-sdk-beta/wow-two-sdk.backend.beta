namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation;

/// <summary>Holds options for downloading the free DB-IP Lite database.</summary>
/// <remarks>DB-IP Lite is licensed CC BY 4.0: the product must show "IP Geolocation by DB-IP" linking to https://db-ip.com.</remarks>
public sealed record DbIpLiteOptions
{
    /// <summary>Gets or sets the writable directory that holds the downloaded database.</summary>
    public string DatabaseDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the database precision. Defaults to <see cref="DbIpLiteEdition.Country"/>.</summary>
    public DbIpLiteEdition Edition { get; set; } = DbIpLiteEdition.Country;

    /// <summary>Gets or sets how often the host checks for a newer monthly release. Defaults to one day.</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Gets or sets the download URL format: <c>{0}</c> is the edition and <c>{1}</c> the release month.</summary>
    public string DownloadUrlFormat { get; set; } = "https://download.db-ip.com/free/dbip-{0}-lite-{1:yyyy-MM}.mmdb.gz";

    /// <summary>Gets or sets the largest decompressed database accepted. Defaults to 512 MiB.</summary>
    public long MaxDatabaseBytes { get; set; } = 512L * 1024 * 1024;

    /// <summary>Gets or sets whether the host downloads releases. Defaults to <see langword="true"/>.</summary>
    /// <remarks>Disable to serve only a file already in <see cref="DatabaseDirectory"/>, such as one a test or an image provides.</remarks>
    public bool EnableDownload { get; set; } = true;
}
