using System.Threading;
using System.Windows;
using MultiDownloader.App.ViewModels;
using MultiDownloader.Core.Extractors;
using MultiDownloader.Core.Http;
using MultiDownloader.Core.Models;

namespace MultiDownloader.App.Views;

public partial class AddDownloadWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly YtDlpClient _ytDlpClient;

    private DownloadKind? _resolvedKind;
    private string? _formatSelector;
    private bool _extractAudioOnly;
    private string? _audioFormat;
    private string? _resolvedTitle;

    public AddDownloadWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _ytDlpClient = new YtDlpClient(viewModel.Bootstrapper, viewModel.Settings);
        FolderTextBox.Text = viewModel.Settings.DownloadFolder;
        ConnectionsTextBox.Text = viewModel.Settings.DefaultConnectionsPerFile.ToString();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = FolderTextBox.Text };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void Fetch_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url))
        {
            StatusText.Text = "أدخل رابطًا أولًا.";
            return;
        }

        FetchButton.IsEnabled = false;
        StatusText.Text = "جارٍ الفحص...";
        _resolvedKind = null;
        _formatSelector = null;
        _extractAudioOnly = false;

        try
        {
            var probe = await _ytDlpClient.ProbeAsync(url, CancellationToken.None);
            var picker = new FormatPickerWindow(probe) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                _resolvedKind = DownloadKind.MediaSite;
                _formatSelector = picker.SelectedFormatSelector;
                _extractAudioOnly = picker.ExtractAudioOnly;
                _audioFormat = picker.AudioFormat;
                _resolvedTitle = probe.Title;
                StatusText.Text = $"سيُنزَّل كـ: {probe.Title} ({(_extractAudioOnly ? "صوت " + _audioFormat : _formatSelector)})";
            }
            else
            {
                StatusText.Text = "لم يُحدَّد صيغة بعد — يمكنك الإضافة مباشرة وسيُختار أفضل جودة تلقائيًا.";
            }
        }
        catch (UnsupportedUrlException)
        {
            try
            {
                var rangeProbe = await RangeProbe.ProbeAsync(_viewModel.Http, url, CancellationToken.None);
                _resolvedKind = DownloadKind.DirectFile;
                var sizeText = rangeProbe.ContentLength is > 0 ? $"{rangeProbe.ContentLength.Value / 1024.0 / 1024:0.#} MB" : "غير معروف";
                var resumeText = rangeProbe.SupportsRanges ? "يدعم الاستئناف والتنزيل المجزّأ" : "اتصال واحد فقط (الخادم لا يدعم النطاقات)";
                StatusText.Text = $"ملف مباشر: {rangeProbe.SuggestedFileName} — الحجم: {sizeText} — {resumeText}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"تعذّر فحص الرابط: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"تعذّر فحص الرابط: {ex.Message}";
        }
        finally
        {
            FetchButton.IsEnabled = true;
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url))
        {
            StatusText.Text = "أدخل رابطًا أولًا.";
            return;
        }
        if (!int.TryParse(ConnectionsTextBox.Text, out var connections) || connections < 1)
        {
            connections = _viewModel.Settings.DefaultConnectionsPerFile;
        }
        connections = Math.Clamp(connections, 1, 16);

        var folder = string.IsNullOrWhiteSpace(FolderTextBox.Text) ? _viewModel.Settings.DownloadFolder : FolderTextBox.Text;

        _viewModel.QueueManager.Add(
            url,
            destinationFolder: folder,
            connections: connections,
            formatSelector: _formatSelector,
            extractAudioOnly: _extractAudioOnly,
            audioFormat: _audioFormat,
            knownKind: _resolvedKind,
            title: _resolvedTitle);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
