using System.Text.Json.Serialization;
using MultiDownloader.Core.Models;

namespace MultiDownloader.Core.Extractors;

public sealed class YtDlpFormatJson
{
    [JsonPropertyName("format_id")] public string FormatId { get; set; } = "";
    [JsonPropertyName("ext")] public string? Ext { get; set; }
    [JsonPropertyName("format_note")] public string? FormatNote { get; set; }
    [JsonPropertyName("resolution")] public string? Resolution { get; set; }
    [JsonPropertyName("vcodec")] public string? Vcodec { get; set; }
    [JsonPropertyName("acodec")] public string? Acodec { get; set; }
    [JsonPropertyName("fps")] public double? Fps { get; set; }
    [JsonPropertyName("filesize")] public long? FileSize { get; set; }
    [JsonPropertyName("filesize_approx")] public long? FileSizeApprox { get; set; }
    [JsonPropertyName("tbr")] public double? Tbr { get; set; }

    public MediaFormat ToMediaFormat() => new()
    {
        FormatId = FormatId,
        Extension = Ext,
        Note = FormatNote,
        Resolution = Resolution,
        VideoCodec = Vcodec,
        AudioCodec = Acodec,
        Fps = Fps,
        ApproxBytes = FileSize ?? FileSizeApprox,
        BitrateKbps = Tbr,
    };
}

public sealed class YtDlpProbeResultJson
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("ext")] public string? Ext { get; set; }
    [JsonPropertyName("formats")] public List<YtDlpFormatJson>? Formats { get; set; }
    [JsonPropertyName("_type")] public string? Type { get; set; }
}

public sealed class YtDlpProbeResult
{
    public required string Title { get; init; }
    public required List<MediaFormat> Formats { get; init; }
}

/// <summary>Thrown when yt-dlp itself reports the URL isn't one it can extract — the caller
/// falls back to treating it as a plain direct-download URL.</summary>
public sealed class UnsupportedUrlException : Exception
{
    public UnsupportedUrlException(string message) : base(message) { }
}
