using System.Diagnostics;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox; // disambiguate vs System.Windows.Forms.MessageBox
using MultiDownloader.App.ViewModels;
using MultiDownloader.App.Views;
using MultiDownloader.Core.Settings;

namespace MultiDownloader.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private void AddDownload_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDownloadWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_viewModel.Settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            SettingsStore.Save(_viewModel.Settings);
        }
    }

    private void PauseAll_Click(object sender, RoutedEventArgs e) => _viewModel.QueueManager.PauseAll();

    private void ResumeAll_Click(object sender, RoutedEventArgs e) => _viewModel.QueueManager.ResumeAll();

    private void PauseItem_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DownloadItemViewModel vm)
            _viewModel.QueueManager.Pause(vm.Id);
    }

    private void ResumeItem_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DownloadItemViewModel vm)
            _viewModel.QueueManager.Resume(vm.Id);
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DownloadItemViewModel vm)
        {
            if (MessageBox.Show(this, $"إزالة \"{vm.DisplayName}\" من القائمة؟", "تأكيد",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _viewModel.QueueManager.Remove(vm.Id);
            }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DownloadItemViewModel vm)
        {
            var folder = vm.Model.DestinationFolder;
            if (Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(this, "المجلد غير موجود.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
