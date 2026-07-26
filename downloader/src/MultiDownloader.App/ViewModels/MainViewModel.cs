using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading;
using Application = System.Windows.Application; // disambiguate vs System.Windows.Forms.Application (WinForms is used only for FolderBrowserDialog)
using MultiDownloader.Core.Extractors;
using MultiDownloader.Core.Queue;
using MultiDownloader.Core.Settings;

namespace MultiDownloader.App.ViewModels;

public sealed class MainViewModel
{
    public DownloadQueueManager QueueManager { get; }
    public AppSettings Settings { get; }
    public HttpClient Http { get; }
    public YtDlpBootstrapper Bootstrapper { get; }

    public ObservableCollection<DownloadItemViewModel> Items { get; } = new();

    public RelayCommand PauseAllCommand { get; }
    public RelayCommand ResumeAllCommand { get; }

    public MainViewModel()
    {
        Settings = SettingsStore.Load();
        Http = new HttpClient();
        Bootstrapper = new YtDlpBootstrapper(Http);
        QueueManager = new DownloadQueueManager(Settings, Http, Bootstrapper);

        foreach (var item in QueueManager.Items)
        {
            Items.Add(new DownloadItemViewModel(item));
        }

        QueueManager.ItemAdded += item => RunOnUi(() => Items.Add(new DownloadItemViewModel(item)));
        QueueManager.ItemRemoved += item => RunOnUi(() =>
        {
            var vm = Items.FirstOrDefault(v => v.Id == item.Id);
            if (vm is not null)
            {
                Items.Remove(vm);
                vm.Dispose();
            }
        });

        PauseAllCommand = new RelayCommand(() => QueueManager.PauseAll());
        ResumeAllCommand = new RelayCommand(() => QueueManager.ResumeAll());

        QueueManager.Pump();
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
