using System.Text.Json;
using MultiDownloader.Core.Models;
using MultiDownloader.Core.Settings;

namespace MultiDownloader.Core.Queue;

/// <summary>Snapshot of a DownloadItem's persistable fields — used to restore the queue list
/// (not in-flight network state, which lives in the per-file SegmentedFileDownloader sidecar)
/// across app restarts.</summary>
public sealed class PersistedDownloadItem
{
    public Guid Id { get; set; }
    public string Url { get; set; } = "";
    public DownloadKind Kind { get; set; }
    public string DestinationFolder { get; set; } = "";
    public string? Title { get; set; }
    public string? FileName { get; set; }
    public int Connections { get; set; } = 8;
    public string? FormatSelector { get; set; }
    public bool ExtractAudioOnly { get; set; }
    public string? AudioFormat { get; set; }
    public DownloadStatus Status { get; set; }
    public long? TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public static class QueueStateStore
{
    private static string QueuePath => Path.Combine(SettingsStore.AppDataFolder, "queue.json");

    public static void Save(IEnumerable<DownloadItem> items)
    {
        var snapshot = items.Select(i => new PersistedDownloadItem
        {
            Id = i.Id,
            Url = i.Url,
            Kind = i.Kind,
            DestinationFolder = i.DestinationFolder,
            Title = i.Title,
            FileName = i.FileName,
            Connections = i.Connections,
            FormatSelector = i.FormatSelector,
            ExtractAudioOnly = i.ExtractAudioOnly,
            AudioFormat = i.AudioFormat,
            // A download that was actively running when the app closed/crashed didn't get a
            // clean pause — mark it Paused so it shows as resumable rather than stuck "Downloading".
            Status = i.Status is DownloadStatus.Downloading or DownloadStatus.Probing or DownloadStatus.Merging
                ? DownloadStatus.Paused
                : i.Status,
            TotalBytes = i.TotalBytes,
            DownloadedBytes = i.DownloadedBytes,
            CreatedAtUtc = i.CreatedAtUtc,
        }).ToList();

        Directory.CreateDirectory(SettingsStore.AppDataFolder);
        var tmp = QueuePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, QueuePath, overwrite: true);
    }

    public static List<DownloadItem> Load()
    {
        if (!File.Exists(QueuePath)) return new List<DownloadItem>();
        try
        {
            var snapshot = JsonSerializer.Deserialize<List<PersistedDownloadItem>>(File.ReadAllText(QueuePath))
                ?? new List<PersistedDownloadItem>();

            return snapshot.Select(p => new DownloadItem
            {
                Id = p.Id,
                Url = p.Url,
                Kind = p.Kind,
                DestinationFolder = p.DestinationFolder,
                Title = p.Title,
                FileName = p.FileName,
                Connections = p.Connections,
                FormatSelector = p.FormatSelector,
                ExtractAudioOnly = p.ExtractAudioOnly,
                AudioFormat = p.AudioFormat,
                Status = p.Status,
                TotalBytes = p.TotalBytes,
                DownloadedBytes = p.DownloadedBytes,
                CreatedAtUtc = p.CreatedAtUtc,
            }).ToList();
        }
        catch
        {
            return new List<DownloadItem>();
        }
    }
}
