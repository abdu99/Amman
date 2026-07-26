using System.Windows;
using MultiDownloader.Core.Extractors;
using MultiDownloader.Core.Models;

namespace MultiDownloader.App.Views;

public partial class FormatPickerWindow : Window
{
    public string? SelectedFormatSelector { get; private set; }
    public bool ExtractAudioOnly { get; private set; }
    public string? AudioFormat { get; private set; }

    public FormatPickerWindow(YtDlpProbeResult probe)
    {
        InitializeComponent();
        TitleText.Text = probe.Title;
        FormatsListView.ItemsSource = probe.Formats
            .OrderByDescending(f => f.BitrateKbps ?? 0)
            .Select(f => new FormatRow(f))
            .ToList();
    }

    private void BestBoth_Click(object sender, RoutedEventArgs e)
    {
        SelectedFormatSelector = "bv*+ba/b";
        ExtractAudioOnly = false;
        DialogResult = true;
        Close();
    }

    private void BestVideoOnly_Click(object sender, RoutedEventArgs e)
    {
        SelectedFormatSelector = "bv*";
        ExtractAudioOnly = false;
        DialogResult = true;
        Close();
    }

    private void AudioOnly_Click(object sender, RoutedEventArgs e)
    {
        SelectedFormatSelector = "bestaudio/best";
        ExtractAudioOnly = true;
        AudioFormat = "mp3";
        DialogResult = true;
        Close();
    }

    private void ChooseSelected_Click(object sender, RoutedEventArgs e)
    {
        if (FormatsListView.SelectedItem is not FormatRow row)
        {
            MessageBox.Show(this, "اختر صيغة من القائمة أولًا، أو استخدم أحد الأزرار السريعة أعلاه.", "تنبيه",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var format = row.Format;
        SelectedFormatSelector = format.HasVideo && !format.HasAudio
            ? $"{format.FormatId}+bestaudio/best"
            : format.FormatId;
        ExtractAudioOnly = false;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class FormatRow
    {
        public MediaFormat Format { get; }
        public string Display => Format.DisplayLabel;
        public FormatRow(MediaFormat format) => Format = format;
        public override string ToString() => Display;
    }
}
