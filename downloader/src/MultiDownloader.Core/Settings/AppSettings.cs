namespace MultiDownloader.Core.Settings;

public sealed class AppSettings
{
    public string DownloadFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public int MaxConcurrentDownloads { get; set; } = 3;
    public int DefaultConnectionsPerFile { get; set; } = 8;

    /// <summary>Overrides auto-detection when set (path to yt-dlp.exe).</summary>
    public string? YtDlpPath { get; set; }

    /// <summary>Overrides auto-detection when set (path to ffmpeg.exe, used by yt-dlp to mux/convert).</summary>
    public string? FfmpegPath { get; set; }
}
