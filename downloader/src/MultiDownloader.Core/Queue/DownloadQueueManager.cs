using System.Collections.Concurrent;
using MultiDownloader.Core.Engine;
using MultiDownloader.Core.Extractors;
using MultiDownloader.Core.Models;
using MultiDownloader.Core.Settings;

namespace MultiDownloader.Core.Queue;

/// <summary>
/// Owns the download queue: adding items, capping how many run at once (multiple simultaneous
/// downloads, each independently pausable/resumable/cancelable), dispatching each item to the
/// right engine (segmented HTTP downloader for direct files, yt-dlp for YouTube/site pages),
/// and persisting the queue so it survives an app restart.
/// </summary>
public sealed class DownloadQueueManager
{
    private readonly List<DownloadItem> _items = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _activeTokens = new();
    private readonly ConcurrentDictionary<Guid, byte> _running = new();
    private readonly object _lock = new();

    private readonly AppSettings _settings;
    private readonly HttpClient _http;
    private readonly SegmentedFileDownloader _fileDownloader;
    private readonly YtDlpClient _ytDlpClient;

    public event Action<DownloadItem>? ItemAdded;
    public event Action<DownloadItem>? ItemRemoved;

    public DownloadQueueManager(AppSettings settings, HttpClient http, YtDlpBootstrapper bootstrapper)
    {
        _settings = settings;
        _http = http;
        _fileDownloader = new SegmentedFileDownloader(http);
        _ytDlpClient = new YtDlpClient(bootstrapper, settings);

        foreach (var item in QueueStateStore.Load())
        {
            _items.Add(item);
        }
    }

    public IReadOnlyList<DownloadItem> Items
    {
        get { lock (_lock) return _items.ToList(); }
    }

    public DownloadItem Add(string url, string? destinationFolder = null, int? connections = null,
        string? formatSelector = null, bool extractAudioOnly = false, string? audioFormat = null,
        DownloadKind? knownKind = null, string? title = null)
    {
        var item = new DownloadItem
        {
            Url = url,
            DestinationFolder = destinationFolder ?? _settings.DownloadFolder,
            Connections = connections ?? _settings.DefaultConnectionsPerFile,
            FormatSelector = formatSelector,
            ExtractAudioOnly = extractAudioOnly,
            AudioFormat = audioFormat,
            Title = title,
            Kind = knownKind ?? (formatSelector is not null || extractAudioOnly ? DownloadKind.MediaSite : DownloadKind.Unknown),
        };

        lock (_lock) _items.Add(item);
        ItemAdded?.Invoke(item);
        PersistQueue();
        Pump();
        return item;
    }

    public void Remove(Guid id)
    {
        Cancel(id);
        DownloadItem? removed = null;
        lock (_lock)
        {
            var idx = _items.FindIndex(i => i.Id == id);
            if (idx >= 0)
            {
                removed = _items[idx];
                _items.RemoveAt(idx);
            }
        }
        if (removed is not null)
        {
            ItemRemoved?.Invoke(removed);
            PersistQueue();
        }
    }

    public void Pause(Guid id)
    {
        lock (_lock)
        {
            if (_activeTokens.TryGetValue(id, out var cts)) cts.Cancel();
        }
    }

    public void Resume(Guid id)
    {
        DownloadItem? item;
        lock (_lock) item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null || _running.ContainsKey(id)) return;
        if (item.Status is DownloadStatus.Completed) return;

        item.Status = DownloadStatus.Queued;
        item.ErrorMessage = null;
        item.RaiseChanged();
        Pump();
    }

    public void PauseAll()
    {
        lock (_lock)
        {
            foreach (var cts in _activeTokens.Values) cts.Cancel();
        }
    }

    public void ResumeAll()
    {
        List<DownloadItem> toResume;
        lock (_lock)
        {
            toResume = _items.Where(i => i.Status is DownloadStatus.Paused or DownloadStatus.Failed).ToList();
        }
        foreach (var item in toResume)
        {
            item.Status = DownloadStatus.Queued;
            item.ErrorMessage = null;
            item.RaiseChanged();
        }
        Pump();
    }

    private void Cancel(Guid id)
    {
        lock (_lock)
        {
            if (_activeTokens.TryGetValue(id, out var cts)) cts.Cancel();
        }
    }

    /// <summary>Starts as many queued items as the concurrency cap allows.</summary>
    public void Pump()
    {
        List<DownloadItem> toStart = new();
        lock (_lock)
        {
            int activeCount = _running.Count;
            int capacity = Math.Max(0, _settings.MaxConcurrentDownloads - activeCount);
            if (capacity == 0) return;

            var candidates = _items
                .Where(i => i.Status == DownloadStatus.Queued && !_running.ContainsKey(i.Id))
                .OrderBy(i => i.CreatedAtUtc)
                .Take(capacity);
            toStart.AddRange(candidates);
        }

        foreach (var item in toStart)
        {
            StartItem(item);
        }
    }

    private void StartItem(DownloadItem item)
    {
        var cts = new CancellationTokenSource();
        lock (_lock) _activeTokens[item.Id] = cts;
        _running[item.Id] = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                await RunItemAsync(item, cts.Token);
            }
            finally
            {
                lock (_lock) _activeTokens.Remove(item.Id);
                _running.TryRemove(item.Id, out _);
                cts.Dispose();
                PersistQueue();
                Pump();
            }
        });
    }

    private async Task RunItemAsync(DownloadItem item, CancellationToken ct)
    {
        try
        {
            if (item.Kind == DownloadKind.Unknown)
            {
                await ResolveKindAsync(item, ct);
            }

            if (item.Kind == DownloadKind.MediaSite)
            {
                await _ytDlpClient.DownloadAsync(item, ct);
            }
            else
            {
                await _fileDownloader.DownloadAsync(item, ct);
            }
        }
        catch (OperationCanceledException)
        {
            item.Status = DownloadStatus.Paused;
            item.RaiseChanged();
        }
        catch (Exception ex)
        {
            item.Status = DownloadStatus.Failed;
            item.ErrorMessage = ex.Message;
            item.RaiseChanged();
        }
    }

    /// <summary>Tries yt-dlp first (covers YouTube + ~1800 other sites); if it reports the URL
    /// as unsupported, treats it as a plain direct-download file instead.</summary>
    private async Task ResolveKindAsync(DownloadItem item, CancellationToken ct)
    {
        item.Status = DownloadStatus.Probing;
        item.RaiseChanged();
        try
        {
            var probe = await _ytDlpClient.ProbeAsync(item.Url, ct);
            item.Kind = DownloadKind.MediaSite;
            item.Title = probe.Title;
            item.FormatSelector ??= "bv*+ba/b";
        }
        catch (UnsupportedUrlException)
        {
            item.Kind = DownloadKind.DirectFile;
        }
    }

    private void PersistQueue()
    {
        lock (_lock) QueueStateStore.Save(_items);
    }
}
