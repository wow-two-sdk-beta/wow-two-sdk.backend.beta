using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Sniffs the <see cref="CaptionFormat"/> of a caption document from its leading content.</summary>
public static partial class CaptionFormatDetector
{
    [GeneratedRegex(@"\d{1,2}:\d{2}:\d{2},\d{3}\s*-->")]
    private static partial Regex SubRipTiming();

    /// <summary>
    /// Attempts to detect the caption format: <c>WEBVTT</c> header → WebVtt, a <c>{ "events": … }</c> root →
    /// json3, a <c>&lt;tt&gt;</c>/TTML root → Ttml, comma-decimal <c>--&gt;</c> timings → SubRip, dot-decimal
    /// <c>--&gt;</c> → WebVtt.
    /// </summary>
    /// <param name="content">The caption document text.</param>
    /// <param name="format">The detected format when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a format was recognized; otherwise <see langword="false"/>.</returns>
    public static bool TryDetect(string content, out CaptionFormat format)
    {
        format = default;
        if (string.IsNullOrWhiteSpace(content)) return false;

        var trimmed = content.TrimStart('﻿', ' ', '\t', '\r', '\n');

        if (trimmed.StartsWith("WEBVTT", StringComparison.Ordinal))
        {
            format = CaptionFormat.WebVtt;
            return true;
        }

        if (trimmed.StartsWith('{'))
        {
            if (trimmed.Contains("\"events\"", StringComparison.Ordinal))
            {
                format = CaptionFormat.YouTubeJson3;
                return true;
            }

            return false;
        }

        if (trimmed.StartsWith('<'))
        {
            if (trimmed.Contains("<tt", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("ttml", StringComparison.OrdinalIgnoreCase))
            {
                format = CaptionFormat.Ttml;
                return true;
            }

            return false;
        }

        if (SubRipTiming().IsMatch(trimmed))
        {
            format = CaptionFormat.SubRip;
            return true;
        }

        if (trimmed.Contains("-->", StringComparison.Ordinal))
        {
            format = CaptionFormat.WebVtt;
            return true;
        }

        return false;
    }
}
