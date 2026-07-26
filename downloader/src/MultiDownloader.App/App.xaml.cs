// UseWPF and UseWindowsForms are both enabled in this project (WinForms only for
// FolderBrowserDialog), and their implicit global usings both bring in an "Application"
// type (System.Windows.Application vs System.Windows.Forms.Application) — alias it to
// disambiguate instead of a plain `using System.Windows;`.
using Application = System.Windows.Application;

namespace MultiDownloader.App;

public partial class App : Application
{
}
