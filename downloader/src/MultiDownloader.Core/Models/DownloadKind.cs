namespace MultiDownloader.Core.Models;

public enum DownloadKind
{
    /// <summary>Not yet resolved — decided the first time the item is probed.</summary>
    Unknown,

    /// <summary>A plain HTTP(S) file (installer, archive, document, direct media file, ...).
    /// Handled by the segmented, resumable <see cref="Engine.SegmentedFileDownloader"/>.</summary>
    DirectFile,

    /// <summary>A page on a site yt-dlp knows how to extract (YouTube and ~1800 other sites).
    /// Handled by <see cref="Extractors.YtDlpClient"/>.</summary>
    MediaSite,
}
