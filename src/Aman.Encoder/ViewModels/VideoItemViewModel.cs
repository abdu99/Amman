using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Aman.Encoder.ViewModels;

public sealed class VideoItemViewModel : INotifyPropertyChanged
{
    public Guid Id { get; } = Guid.NewGuid();
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string SizeLabel { get; }

    private string _title;
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public VideoItemViewModel(string filePath)
    {
        FilePath = filePath;
        _title = Path.GetFileNameWithoutExtension(filePath);

        long bytes = 0;
        try { bytes = new FileInfo(filePath).Length; } catch { /* file may be on a removable drive that just unmounted */ }
        SizeLabel = bytes >= 1024 * 1024 * 1024
            ? $"{bytes / (1024.0 * 1024 * 1024):0.00} GB"
            : $"{bytes / (1024.0 * 1024):0.0} MB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
