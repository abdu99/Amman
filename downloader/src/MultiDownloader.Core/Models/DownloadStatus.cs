namespace MultiDownloader.Core.Models;

public enum DownloadStatus
{
    Queued,
    Probing,
    Downloading,
    Merging,
    Paused,
    Completed,
    Failed,
    Canceled,
}
