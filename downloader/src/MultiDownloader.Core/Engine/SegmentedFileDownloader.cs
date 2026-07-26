using System.Net;
using System.Net.Http.Headers;
using Microsoft.Win32.SafeHandles;
using MultiDownloader.Core.Http;
using MultiDownloader.Core.Models;

namespace MultiDownloader.Core.Engine;

/// <summary>
/// Downloads a plain HTTP(S) file with multiple concurrent connections (one per byte-range
/// segment) and resumable, restart-safe progress. Progress/segment state is written to a
/// "&lt;file&gt;.part.json" sidecar so pausing (or an app/process crash) never loses completed
/// bytes — resuming re-reads that sidecar and only re-requests what's still missing.
/// </summary>
public sealed class SegmentedFileDownloader
{
    private const long MinBytesPerSegment = 2 * 1024 * 1024; // don't split a file into pointlessly small segments
    private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(1);

    private readonly HttpClient _http;

    public SegmentedFileDownloader(HttpClient http) => _http = http;

    public async Task DownloadAsync(DownloadItem item, CancellationToken ct)
    {
        item.Status = DownloadStatus.Probing;
        item.RaiseChanged();

        var probe = await RangeProbe.ProbeAsync(_http, item.Url, ct);

        item.FileName ??= SanitizeFileName(probe.SuggestedFileName ?? $"download-{item.Id:N}");
        item.Title ??= item.FileName;
        item.TotalBytes ??= probe.ContentLength;

        Directory.CreateDirectory(item.DestinationFolder);
        var finalPath = ResolveNonCollidingPath(Path.Combine(item.DestinationFolder, item.FileName));
        var partPath = finalPath + ".part";

        var state = DownloadStateStore.TryLoad(partPath);
        bool canResume = state is not null && state.Url == item.Url && state.ContentLength == probe.ContentLength;
        if (!canResume)
        {
            state = BuildInitialState(item, probe);
            if (File.Exists(partPath)) File.Delete(partPath);
        }
        else
        {
            item.DownloadedBytes = state!.Segments.Sum(s => s.Downloaded);
        }

        item.Status = DownloadStatus.Downloading;
        item.RaiseChanged();

        using var fileHandle = File.OpenHandle(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite,
            FileOptions.Asynchronous, preallocationSize: probe.ContentLength ?? 0);
        if (probe.ContentLength is > 0)
        {
            RandomAccess.SetLength(fileHandle, probe.ContentLength.Value);
        }

        var lastSave = DateTimeOffset.UtcNow;
        var saveLock = new SemaphoreSlim(1, 1);
        async Task PersistThrottledAsync(bool force)
        {
            if (!force && DateTimeOffset.UtcNow - lastSave < ProgressSaveInterval) return;
            await saveLock.WaitAsync(CancellationToken.None);
            try
            {
                lastSave = DateTimeOffset.UtcNow;
                await DownloadStateStore.SaveAsync(partPath, state!, CancellationToken.None);
            }
            finally
            {
                saveLock.Release();
            }
        }

        long lastBytes = item.DownloadedBytes;
        var lastTick = DateTimeOffset.UtcNow;
        using var speedTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        var speedTask = Task.Run(async () =>
        {
            try
            {
                while (await speedTimer.WaitForNextTickAsync(ct))
                {
                    var now = DateTimeOffset.UtcNow;
                    var elapsed = (now - lastTick).TotalSeconds;
                    if (elapsed > 0)
                    {
                        item.SpeedBytesPerSec = (item.DownloadedBytes - lastBytes) / elapsed;
                        item.Eta = item.TotalBytes is > 0 && item.SpeedBytesPerSec > 0
                            ? TimeSpan.FromSeconds((item.TotalBytes.Value - item.DownloadedBytes) / item.SpeedBytesPerSec)
                            : null;
                        lastBytes = item.DownloadedBytes;
                        lastTick = now;
                        item.RaiseChanged();
                    }
                }
            }
            catch (OperationCanceledException) { /* expected on pause/cancel/completion */ }
        }, CancellationToken.None);

        try
        {
            var pending = state!.Segments.Where(s => s.End < 0 || s.Downloaded < s.End - s.Start + 1).ToList();
            var tasks = pending.Select(segment => DownloadSegmentAsync(item, fileHandle, segment, state, PersistThrottledAsync, ct));
            await Task.WhenAll(tasks);

            ct.ThrowIfCancellationRequested();

            fileHandle.Dispose(); // release the handle before renaming
            File.Move(partPath, finalPath, overwrite: false);
            DownloadStateStore.Delete(partPath);

            item.FileName = Path.GetFileName(finalPath);
            item.DownloadedBytes = item.TotalBytes ?? item.DownloadedBytes;
            item.Status = DownloadStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            await PersistThrottledAsync(force: true);
            item.Status = DownloadStatus.Paused;
        }
        catch (Exception ex)
        {
            await PersistThrottledAsync(force: true);
            item.Status = DownloadStatus.Failed;
            item.ErrorMessage = ex.Message;
        }
        finally
        {
            speedTimer.Dispose();
            try { await speedTask; } catch { /* already logged via item.ErrorMessage if relevant */ }
            item.RaiseChanged();
        }
    }

    private static DownloadState BuildInitialState(DownloadItem item, RangeProbeResult probe)
    {
        var state = new DownloadState
        {
            Url = item.Url,
            ContentLength = probe.ContentLength,
            SupportsRanges = probe.SupportsRanges,
        };

        if (probe.SupportsRanges && probe.ContentLength is > 0)
        {
            long length = probe.ContentLength.Value;
            int segmentCount = Math.Max(1, Math.Min(item.Connections, (int)(length / MinBytesPerSegment)));
            segmentCount = Math.Max(1, segmentCount);
            long chunk = length / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                long start = i * chunk;
                long end = i == segmentCount - 1 ? length - 1 : start + chunk - 1;
                state.Segments.Add(new SegmentState { Start = start, End = end, Downloaded = 0 });
            }
        }
        else
        {
            // Unknown length or no range support: one connection, no internal split.
            state.Segments.Add(new SegmentState { Start = 0, End = -1, Downloaded = 0 });
        }

        return state;
    }

    private async Task DownloadSegmentAsync(
        DownloadItem item,
        SafeFileHandle fileHandle,
        SegmentState segment,
        DownloadState state,
        Func<bool, Task> persist,
        CancellationToken ct)
    {
        long resumeOffset = segment.Start + segment.Downloaded;
        using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
        request.Headers.Range = segment.End >= 0
            ? new RangeHeaderValue(resumeOffset, segment.End)
            : new RangeHeaderValue(resumeOffset, null);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (state.SupportsRanges && response.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new InvalidOperationException(
                $"الخادم لم يستجب بجزء (HTTP {(int)response.StatusCode}) رغم دعمه المُعلَن للنطاقات — أعد المحاولة من جديد.");
        }
        response.EnsureSuccessStatusCode();

        // We asked to resume from resumeOffset via Range, but a server that doesn't actually
        // honor Range (despite us trying anyway when SupportsRanges was false) may reply 200
        // with the full body starting at byte 0. Writing that at resumeOffset would corrupt
        // the file, so detect it and restart this segment from scratch instead.
        if (response.StatusCode == HttpStatusCode.OK && resumeOffset > 0)
        {
            item.AddDownloadedBytes(-segment.Downloaded); // undo the stale count folded in at start-of-run
            segment.Downloaded = 0;
            resumeOffset = segment.Start;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[81920];
        long writeOffset = resumeOffset;

        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await RandomAccess.WriteAsync(fileHandle, buffer.AsMemory(0, read), writeOffset, ct);
            writeOffset += read;
            segment.Downloaded += read;
            item.AddDownloadedBytes(read);
            await persist(false);
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private static string ResolveNonCollidingPath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
