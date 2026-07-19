using System.Windows;
using System.Windows.Threading;

namespace Aman.Player;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "AMAN", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
