namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.Fcm;

/// <summary>Holds the Firebase service account the HTTP v1 API authenticates with.</summary>
public sealed record FcmPushOptions
{
    /// <summary>The service-account key file's JSON (<c>project_id</c>, <c>client_email</c>, <c>private_key</c>, <c>token_uri</c>).</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>API base address. Default <c>https://fcm.googleapis.com/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://fcm.googleapis.com/");
}
