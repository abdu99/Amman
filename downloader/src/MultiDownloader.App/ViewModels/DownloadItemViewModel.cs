using System.ComponentModel;
using Application = System.Windows.Application; // disambiguate vs System.Windows.Forms.Application
using MultiDownloader.Core.Models;

namespace MultiDownloader.App.ViewModels;

public sealed class DownloadItemViewModel : INotifyPropertyChanged, IDisposable
{
    public DownloadItem Model { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DownloadItemViewModel(DownloadItem model)
    {
        Model = model;
        Model.Changed += OnModelChanged;
    }

    private void OnModelChanged(DownloadItem _)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RaiseAll();
        }
        else
        {
            dispatcher.BeginInvoke(RaiseAll);
        }
    }

    private void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public Guid Id => Model.Id;
    public string Url => Model.Url;
    public string DisplayName => Model.Title ?? Model.FileName ?? Model.Url;
    public double ProgressPercent => Model.PercentComplete;
    public string StatusText => Model.Status switch
    {
        DownloadStatus.Queued => "بالانتظار",
        DownloadStatus.Probing => "جارٍ الفحص...",
        DownloadStatus.Downloading => "جارٍ التنزيل",
        DownloadStatus.Merging => "جارٍ الدمج...",
        DownloadStatus.Paused => "متوقف مؤقتًا",
        DownloadStatus.Completed => "اكتمل",
        DownloadStatus.Failed => $"فشل: {Model.ErrorMessage}",
        DownloadStatus.Canceled => "أُلغي",
        _ => Model.Status.ToString(),
    };

    public string SizeText => Model.TotalBytes is > 0
        ? $"{FormatBytes(Model.DownloadedBytes)} / {FormatBytes(Model.TotalBytes.Value)}"
        : FormatBytes(Model.DownloadedBytes);

    public string SpeedText => Model.Status == DownloadStatus.Downloading && Model.SpeedBytesPerSec > 0
        ? $"{FormatBytes((long)Model.SpeedBytesPerSec)}/ث"
        : "";

    public string EtaText => Model.Eta is { } eta && Model.Status == DownloadStatus.Downloading
        ? eta.TotalHours >= 1 ? $"{eta:hh\\:mm\\:ss}" : $"{eta:mm\\:ss}"
        : "";

    public bool CanPause => Model.Status is DownloadStatus.Downloading or DownloadStatus.Probing or DownloadStatus.Merging;
    public bool CanResume => Model.Status is DownloadStatus.Paused or DownloadStatus.Failed;

    private static string FormatBytes(long bytes)
    {
        double v = bytes;
        string[] units = { "B", "KB", "MB", "GB" };
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {units[i]}";
    }

    public void Dispose() => Model.Changed -= OnModelChanged;
}
