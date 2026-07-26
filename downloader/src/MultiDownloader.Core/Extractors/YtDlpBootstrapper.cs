using MultiDownloader.Core.Settings;

namespace MultiDownloader.Core.Extractors;

/// <summary>
/// Locates a usable yt-dlp.exe (and checks for ffmpeg), downloading yt-dlp from its official
/// GitHub release the first time neither is found. We don't vendor the binary in this repo —
/// yt-dlp ships frequent fixes for sites that change their extraction, so always fetching the
/// latest release keeps the app working instead of silently rotting.
/// </summary>
public sealed class YtDlpBootstrapper
{
    private const string LatestReleaseUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

    private readonly HttpClient _http;

    public YtDlpBootstrapper(HttpClient http) => _http = http;

    public string ToolsFolder => Path.Combine(SettingsStore.AppDataFolder, "tools");

    public async Task<string> EnsureYtDlpAsync(AppSettings settings, CancellationToken ct, IProgress<string>? status = null)
    {
        if (!string.IsNullOrWhiteSpace(settings.YtDlpPath) && File.Exists(settings.YtDlpPath))
            return settings.YtDlpPath;

        var nextToApp = Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe");
        if (File.Exists(nextToApp)) return nextToApp;

        var onPath = TryFindOnPath("yt-dlp.exe") ?? TryFindOnPath("yt-dlp");
        if (onPath is not null) return onPath;

        var cached = Path.Combine(ToolsFolder, "yt-dlp.exe");
        if (File.Exists(cached)) return cached;

        status?.Report("جارٍ تنزيل yt-dlp (أداة الاستخراج) من GitHub لأول مرة...");
        Directory.CreateDirectory(ToolsFolder);
        var tmp = cached + ".tmp";
        await using (var httpStream = await _http.GetStreamAsync(LatestReleaseUrl, ct))
        await using (var fileStream = File.Create(tmp))
        {
            await httpStream.CopyToAsync(fileStream, ct);
        }
        File.Move(tmp, cached, overwrite: true);
        return cached;
    }

    /// <summary>Returns the path if ffmpeg is available (needed by yt-dlp to merge separate
    /// video+audio streams or transcode to mp3), or null. We don't auto-download ffmpeg — its
    /// builds are much larger and licensing/packaging varies by build — the UI should suggest
    /// the user installs it or picks a pre-muxed / progressive format instead.</summary>
    public string? TryFindFfmpeg(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.FfmpegPath) && File.Exists(settings.FfmpegPath))
            return settings.FfmpegPath;

        var nextToApp = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
        if (File.Exists(nextToApp)) return nextToApp;

        return TryFindOnPath("ffmpeg.exe") ?? TryFindOnPath("ffmpeg");
    }

    private static string? TryFindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Malformed PATH entry — skip it.
            }
        }
        return null;
    }
}
