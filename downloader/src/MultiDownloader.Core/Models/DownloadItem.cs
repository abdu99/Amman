namespace MultiDownloader.Core.Models;

/// <summary>
/// A single queue entry. Plain data + a change event — deliberately has no WPF/UI
/// dependency so MultiDownloader.Core stays reusable outside the WPF app. The App layer
/// wraps this in a ViewModel that forwards <see cref="Changed"/> onto the UI thread.
/// </summary>
public sealed class DownloadItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Url { get; init; }
    public DownloadKind Kind { get; set; } = DownloadKind.Unknown;
    public required string DestinationFolder { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Resolved once known: video title (MediaSite) or Content-Disposition/URL name (DirectFile).</summary>
    public string? Title { get; set; }

    /// <summary>Final file name on disk, once resolved.</summary>
    public string? FileName { get; set; }

    // --- DirectFile-specific ---
    public int Connections { get; set; } = 8;

    // --- MediaSite-specific ---
    /// <summary>Raw yt-dlp -f selector, e.g. "137+140", "bestaudio/best", "bv*+ba/b".</summary>
    public string? FormatSelector { get; set; }
    /// <summary>True when the user picked an audio-only preset (adds --extract-audio).</summary>
    public bool ExtractAudioOnly { get; set; }
    public string? AudioFormat { get; set; } = "mp3";

    // --- Live progress ---
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public long? TotalBytes { get; set; }

    private long _downloadedBytes;

    /// <summary>Thread-safe: multiple segment tasks read/write this concurrently.</summary>
    public long DownloadedBytes
    {
        get => Interlocked.Read(ref _downloadedBytes);
        set => Interlocked.Exchange(ref _downloadedBytes, value);
    }

    /// <summary>Atomically adds <paramref name="delta"/> bytes — safe to call from any number
    /// of concurrent segment-download tasks at once.</summary>
    public void AddDownloadedBytes(long delta) => Interlocked.Add(ref _downloadedBytes, delta);

    public double SpeedBytesPerSec { get; set; }
    public TimeSpan? Eta { get; set; }
    public string? ErrorMessage { get; set; }

    public double PercentComplete =>
        TotalBytes is > 0 ? Math.Clamp(DownloadedBytes * 100.0 / TotalBytes.Value, 0, 100) : 0;

    /// <summary>Raised (from any thread) whenever a mutable property above changes.</summary>
    public event Action<DownloadItem>? Changed;

    public void RaiseChanged() => Changed?.Invoke(this);
}
