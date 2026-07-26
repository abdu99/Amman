using System.Windows;
using MultiDownloader.Core.Settings;

namespace MultiDownloader.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        FolderTextBox.Text = settings.DownloadFolder;
        MaxConcurrentTextBox.Text = settings.MaxConcurrentDownloads.ToString();
        DefaultConnectionsTextBox.Text = settings.DefaultConnectionsPerFile.ToString();
        YtDlpPathTextBox.Text = settings.YtDlpPath;
        FfmpegPathTextBox.Text = settings.FfmpegPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = FolderTextBox.Text };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(FolderTextBox.Text)) _settings.DownloadFolder = FolderTextBox.Text;

        if (int.TryParse(MaxConcurrentTextBox.Text, out var maxConcurrent) && maxConcurrent >= 1)
            _settings.MaxConcurrentDownloads = Math.Clamp(maxConcurrent, 1, 20);

        if (int.TryParse(DefaultConnectionsTextBox.Text, out var connections) && connections >= 1)
            _settings.DefaultConnectionsPerFile = Math.Clamp(connections, 1, 16);

        _settings.YtDlpPath = string.IsNullOrWhiteSpace(YtDlpPathTextBox.Text) ? null : YtDlpPathTextBox.Text;
        _settings.FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPathTextBox.Text) ? null : FfmpegPathTextBox.Text;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
