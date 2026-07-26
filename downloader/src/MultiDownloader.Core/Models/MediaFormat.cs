namespace MultiDownloader.Core.Models;

/// <summary>One entry from `yt-dlp -j` formats[] — one selectable quality/codec/container option.</summary>
public sealed class MediaFormat
{
    public required string FormatId { get; init; }
    public string? Extension { get; init; }
    public string? Note { get; init; }
    public string? Resolution { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public double? Fps { get; init; }
    public long? ApproxBytes { get; init; }
    public double? BitrateKbps { get; init; }

    public bool HasVideo => !string.IsNullOrEmpty(VideoCodec) && VideoCodec != "none";
    public bool HasAudio => !string.IsNullOrEmpty(AudioCodec) && AudioCodec != "none";

    public string DisplayLabel
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Resolution) && Resolution != "audio only") parts.Add(Resolution!);
            if (Fps is > 0) parts.Add($"{Fps:0}fps");
            parts.Add(HasVideo && HasAudio ? "فيديو+صوت" : HasVideo ? "فيديو فقط" : "صوت فقط");
            if (!string.IsNullOrEmpty(Extension)) parts.Add(Extension!);
            if (!string.IsNullOrEmpty(Note)) parts.Add(Note!);
            if (ApproxBytes is > 0) parts.Add(FormatBytes(ApproxBytes.Value));
            return string.Join(" · ", parts);
        }
    }

    private static string FormatBytes(long bytes)
    {
        double v = bytes;
        string[] units = { "B", "KB", "MB", "GB" };
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {units[i]}";
    }
}
