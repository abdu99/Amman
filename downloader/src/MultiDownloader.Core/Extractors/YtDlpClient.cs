using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MultiDownloader.Core.Models;
using MultiDownloader.Core.Settings;

namespace MultiDownloader.Core.Extractors;

/// <summary>Thin wrapper around the yt-dlp process: probing a URL's available formats, and
/// running the actual download while translating its stdout into <see cref="DownloadItem"/>
/// progress updates. Pausing = killing the process; yt-dlp leaves a ".part" file behind and
/// resumes from it automatically (--continue is the default) when the same command runs again,
/// which is how resume is implemented for this download path.</summary>
public sealed class YtDlpClient
{
    private readonly YtDlpBootstrapper _bootstrapper;
    private readonly AppSettings _settings;

    public YtDlpClient(YtDlpBootstrapper bootstrapper, AppSettings settings)
    {
        _bootstrapper = bootstrapper;
        _settings = settings;
    }

    public async Task<YtDlpProbeResult> ProbeAsync(string url, CancellationToken ct)
    {
        var ytDlpPath = await _bootstrapper.EnsureYtDlpAsync(_settings, ct);

        var (exitCode, stdout, stderr) = await RunAsync(
            ytDlpPath, new[] { "--dump-single-json", "--no-warnings", "--no-playlist", url }, ct);

        if (exitCode != 0 || stdout.Length == 0)
        {
            if (stderr.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase))
                throw new UnsupportedUrlException(stderr);
            throw new InvalidOperationException($"فشل استخراج معلومات الرابط (yt-dlp): {Truncate(stderr)}");
        }

        var parsed = JsonSerializer.Deserialize<YtDlpProbeResultJson>(stdout)
            ?? throw new InvalidOperationException("لم يتمكن yt-dlp من إرجاع بيانات صالحة لهذا الرابط.");

        var formats = (parsed.Formats ?? new List<YtDlpFormatJson>())
            .Select(f => f.ToMediaFormat())
            .Where(f => !string.IsNullOrEmpty(f.FormatId))
            .ToList();

        return new YtDlpProbeResult { Title = parsed.Title ?? parsed.Id ?? url, Formats = formats };
    }

    public async Task DownloadAsync(DownloadItem item, CancellationToken ct)
    {
        var ytDlpPath = await _bootstrapper.EnsureYtDlpAsync(_settings, ct);
        var ffmpegPath = _bootstrapper.TryFindFfmpeg(_settings);

        item.Status = DownloadStatus.Downloading;
        item.RaiseChanged();

        Directory.CreateDirectory(item.DestinationFolder);
        var outputTemplate = Path.Combine(item.DestinationFolder, "%(title)s.%(ext)s");

        var args = new List<string>
        {
            "--newline", "--no-warnings", "--no-playlist", "--continue",
            "-f", item.FormatSelector ?? "bv*+ba/b",
            "-o", outputTemplate,
        };
        if (ffmpegPath is not null) args.AddRange(new[] { "--ffmpeg-location", ffmpegPath });
        if (item.ExtractAudioOnly)
        {
            args.AddRange(new[] { "--extract-audio", "--audio-format", item.AudioFormat ?? "mp3", "--audio-quality", "0" });
        }
        args.Add(item.Url);

        var psi = new ProcessStartInfo(ytDlpPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderrTail = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            var progress = YtDlpProgressParser.Parse(e.Data);
            if (progress is null) return;

            if (progress.Value.DestinationFileName is { } name)
            {
                item.FileName = Path.GetFileName(name);
                item.Title ??= Path.GetFileNameWithoutExtension(name);
            }
            if (progress.Value.IsMerging) item.Status = DownloadStatus.Merging;
            if (progress.Value.TotalBytes is { } total) item.TotalBytes = total;
            if (progress.Value.Percent is { } pct && item.TotalBytes is { } tb)
                item.DownloadedBytes = (long)(tb * pct / 100.0);
            if (progress.Value.SpeedBytesPerSec is { } speed) item.SpeedBytesPerSec = speed;
            item.Eta = progress.Value.Eta;
            item.RaiseChanged();
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderrTail.AppendLine(e.Data);
            if (stderrTail.Length > 4000) stderrTail.Remove(0, stderrTail.Length - 4000);
        };

        using var registration = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* already exited */ }
        });

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(CancellationToken.None);

        if (ct.IsCancellationRequested)
        {
            item.Status = DownloadStatus.Paused; // .part file left in place by yt-dlp for next --continue
            item.RaiseChanged();
            return;
        }

        if (process.ExitCode != 0)
        {
            item.Status = DownloadStatus.Failed;
            item.ErrorMessage = Truncate(stderrTail.ToString());
        }
        else
        {
            item.Status = DownloadStatus.Completed;
            item.DownloadedBytes = item.TotalBytes ?? item.DownloadedBytes;
        }
        item.RaiseChanged();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string exePath, IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[^500..];
}
