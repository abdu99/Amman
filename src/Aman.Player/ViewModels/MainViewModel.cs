using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aman.Player.Services;
using Aman.Shared.Branding;
using Aman.Shared.Container;
using Aman.Shared.Hardware;
using Aman.Shared.Licensing;
using Aman.Shared.Localization;
using Aman.Shared.Theming;

namespace Aman.Player.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private ContainerReader? _reader;
    private readonly Dictionary<Guid, byte[]> _contentKeys = new();
    private SecurePlaybackServer? _server;
    private string _password = string.Empty;
    private string _machineId = string.Empty;

    public ObservableCollection<PlaylistEntryViewModel> Playlist { get; } = new();
    public LocalizationManager Loc => LocalizationManager.Instance;

    public string FatalError { get; private set; } = string.Empty;
    public bool HasFatalError => !string.IsNullOrEmpty(FatalError);

    private string _title = "AMAN";
    public string Title { get => _title; private set => SetField(ref _title, value); }

    private BitmapImage? _logo;
    public BitmapImage? Logo { get => _logo; private set => SetField(ref _logo, value); }

    public bool RequireActivationCode { get; private set; }

    public string MachineIdDisplay { get => _machineId; private set => SetField(ref _machineId, value); }

    public string ActivationCode { get; set; } = string.Empty;

    private bool _isUnlocked;
    public bool IsUnlocked { get => _isUnlocked; private set => SetField(ref _isUnlocked, value); }

    private string _errorMessage = string.Empty;
    public string ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    private PlaylistEntryViewModel? _selectedEntry;
    public PlaylistEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!SetField(ref _selectedEntry, value)) return;
            CurrentVideoUri = value is not null && _server is not null ? _server.GetEntryUri(value.Id) : null;
        }
    }

    private Uri? _currentVideoUri;
    public Uri? CurrentVideoUri { get => _currentVideoUri; private set => SetField(ref _currentVideoUri, value); }

    public ICommand UnlockCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }

    public MainViewModel()
    {
        UnlockCommand = new RelayCommand(_ => TryUnlock());
        ToggleLanguageCommand = new RelayCommand(_ =>
            Loc.CurrentLanguage = Loc.CurrentLanguage == AppLanguage.Arabic ? AppLanguage.English : AppLanguage.Arabic);
        NextCommand = new RelayCommand(_ => Advance(+1), _ => CanAdvance(+1));
        PreviousCommand = new RelayCommand(_ => Advance(-1), _ => CanAdvance(-1));

        LoadContainer();
    }

    public void SetPassword(string password) => _password = password;

    private void LoadContainer()
    {
        try
        {
            string exePath = SelfExeLocator.GetCurrentExecutablePath();
            _reader = ContainerReader.Open(exePath);
            _reader.VerifyHeaderIntegrity();
        }
        catch (Exception ex)
        {
            FatalError = ex.Message;
            return;
        }

        var branding = _reader.Header.Branding;
        Title = string.IsNullOrWhiteSpace(branding.Title) ? "AMAN" : branding.Title;
        RequireActivationCode = (_reader.Header.Flags & ContainerConstants.HeaderFlags.RequireActivationCode) != 0;

        ApplyTheme(branding);

        if (branding.LogoPng.Length > 0)
        {
            using var ms = new MemoryStream(branding.LogoPng);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            Logo = bmp;
        }

        MachineIdDisplay = HardwareIdProvider.GetMachineId();
    }

    private static void ApplyTheme(BrandingInfo branding)
    {
        var uri = branding.Theme == ContainerConstants.ThemeMode.Light
            ? new Uri("pack://application:,,,/Themes/Colors.Light.xaml")
            : new Uri("pack://application:,,,/Themes/Colors.Dark.xaml"); // "System" falls back to Dark for now.
        ThemeManager.Apply(uri);

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(branding.AccentColorHex)!;
            Application.Current.Resources["AccentColor"] = color;
            Application.Current.Resources["AccentBrush"] = new SolidColorBrush(color);
        }
        catch
        {
            // Malformed accent hex from a hand-edited branding value — keep the theme's default accent.
        }
    }

    private void TryUnlock()
    {
        ErrorMessage = string.Empty;
        if (_reader is null) return;

        if (!_reader.TryVerifyPassword(_password))
        {
            ErrorMessage = Loc["wrong_password"];
            return;
        }

        LicenseToken? token = null;
        if (RequireActivationCode)
        {
            token = ActivationCodeCodec.TryDecodeAndVerify(ActivationCode, _reader.Header.VendorPublicKey);
            if (token is null)
            {
                ErrorMessage = Loc["invalid_activation_code"];
                return;
            }
        }

        int runsSoFar = RunStateStore.GetRunCount(_reader.Header.PackageId);
        var nowUtc = DateTime.UtcNow;

        if (_reader.Header.ExpiresUtc.HasValue && nowUtc > _reader.Header.ExpiresUtc.Value)
        {
            ErrorMessage = Loc["license_expired"];
            return;
        }
        if (_reader.Header.MaxRuns.HasValue && runsSoFar >= _reader.Header.MaxRuns.Value)
        {
            ErrorMessage = Loc["license_run_limit"];
            return;
        }

        if (token is not null)
        {
            var status = LicenseEvaluator.Evaluate(token, _reader.Header.PackageId, _machineId, nowUtc, runsSoFar);
            if (status != LicenseStatus.Valid)
            {
                ErrorMessage = status switch
                {
                    LicenseStatus.WrongMachine => Loc["license_wrong_machine"],
                    LicenseStatus.Expired => Loc["license_expired"],
                    LicenseStatus.RunLimitReached => Loc["license_run_limit"],
                    _ => Loc["invalid_activation_code"],
                };
                return;
            }
        }

        foreach (var entry in _reader.Header.Entries)
            _contentKeys[entry.Id] = _reader.UnwrapContentKey(entry, _password);

        RunStateStore.RecordRun(_reader.Header.PackageId);

        _server = new SecurePlaybackServer(_reader, _contentKeys);
        _server.Start();

        int index = 0;
        foreach (var entry in _reader.Header.Entries)
            Playlist.Add(new PlaylistEntryViewModel { Id = entry.Id, Title = entry.Title, Index = index++ });

        IsUnlocked = true;
        SelectedEntry = Playlist.FirstOrDefault();
    }

    private bool CanAdvance(int delta)
    {
        if (SelectedEntry is null) return false;
        int next = SelectedEntry.Index + delta;
        return next >= 0 && next < Playlist.Count;
    }

    private void Advance(int delta)
    {
        if (SelectedEntry is null) return;
        int next = SelectedEntry.Index + delta;
        if (next >= 0 && next < Playlist.Count)
            SelectedEntry = Playlist[next];
    }

    public void Dispose()
    {
        _server?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
