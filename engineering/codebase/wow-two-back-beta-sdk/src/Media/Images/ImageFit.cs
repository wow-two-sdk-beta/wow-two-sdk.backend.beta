namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Refers to how an image meets a target width and height.</summary>
public enum ImageFit
{
    /// <summary>Scale to fit inside the box, keeping the aspect ratio; the result may be smaller on one side.</summary>
    Max,

    /// <summary>Scale to fill the box and crop the overflow, centered; the result is exactly the box.</summary>
    Cover,

    /// <summary>Scale to fit inside the box and pad the rest with the background; the result is exactly the box.</summary>
    Pad,

    /// <summary>Stretch to the box, ignoring the aspect ratio.</summary>
    Stretch,
}
