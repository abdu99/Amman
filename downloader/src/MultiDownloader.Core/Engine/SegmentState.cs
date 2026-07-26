using System.Text.Json;

namespace MultiDownloader.Core.Engine;

public sealed class SegmentState
{
    public long Start { get; set; }
    /// <summary>Inclusive end offset, or -1 when the total length is unknown (single, unsplit segment).</summary>
    public long End { get; set; }
    public long Downloaded { get; set; }
}

/// <summary>Persisted next to the partial file as "&lt;file&gt;.part.json" so a download can
/// resume — even across app restarts — without re-downloading bytes already on disk.</summary>
public sealed class DownloadState
{
    public required string Url { get; set; }
    public long? ContentLength { get; set; }
    public bool SupportsRanges { get; set; }
    public List<SegmentState> Segments { get; set; } = new();
}

public static class DownloadStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static string SidecarPath(string partFilePath) => partFilePath + ".json";

    public static async Task SaveAsync(string partFilePath, DownloadState state, CancellationToken ct)
    {
        var path = SidecarPath(partFilePath);
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, ct);
        }
        File.Move(tmp, path, overwrite: true);
    }

    public static DownloadState? TryLoad(string partFilePath)
    {
        var path = SidecarPath(partFilePath);
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<DownloadState>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Delete(string partFilePath)
    {
        var path = SidecarPath(partFilePath);
        if (File.Exists(path)) File.Delete(path);
    }
}
