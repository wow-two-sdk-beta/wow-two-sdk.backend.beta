namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Defines a span of media text with its offsets — a caption cue, a speech segment, a transcript line.</summary>
/// <remarks>Slicing and text rendering work over any span, so a product's own segment type joins by implementing it.</remarks>
public interface ITimedText
{
    /// <summary>Gets the offset from the media start where the span begins.</summary>
    TimeSpan Start { get; }

    /// <summary>Gets the offset from the media start where the span ends.</summary>
    TimeSpan End { get; }

    /// <summary>Gets the spoken or captioned text.</summary>
    string Text { get; }
}
