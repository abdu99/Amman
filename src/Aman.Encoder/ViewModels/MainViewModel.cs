using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Aman.Encoder.Models;
using Aman.Encoder.Services;
using Aman.Shared.Branding;
using Aman.Shared.Container;
using Aman.Shared.Licensing;
using Aman.Shared.Localization;
using Microsoft.Win32;

namespace Aman.Encoder.ViewModels;

public enum ProtectionMode { PasswordOnly, ActivationCode }

public sealed class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<VideoItemViewModel> Videos { get; } = new();
    public ObservableCollection<PackageHistoryEntry> BuildHistory { get; } = new();

    private readonly PackageBuilderService _packageBuilder = new();
    private readonly VendorIdentity _vendorIdentity = VendorIdentityStore.LoadOrCreate();

    public string VendorPublicKeyFingerprint { get; }

    // --- Branding ---
    private string _playerTitle = "AMAN";
    public string PlayerTitle { get => _playerTitle; set => SetField(ref _playerTitle, value); }

    private string _logoPath = string.Empty;
    public string LogoPath { get => _logoPath; set => SetField(ref _logoPath, value); }

    private string _accentColorHex = "#2E7D32";
    public string AccentColorHex { get => _accentColorHex; set => SetField(ref _accentColorHex, value); }

    private ContainerConstants.ThemeMode _selectedTheme = ContainerConstants.ThemeMode.Dark;
    public ContainerConstants.ThemeMode SelectedTheme { get => _selectedTheme; set => SetField(ref _selectedTheme, value); }

    // --- Protection ---
    private string _password = string.Empty;
    public string Password { get => _password; set => SetField(ref _password, value); }

    private string _confirmPassword = string.Empty;
    public string ConfirmPassword { get => _confirmPassword; set => SetField(ref _confirmPassword, value); }

    private ProtectionMode _selectedProtectionMode = ProtectionMode.PasswordOnly;
    public ProtectionMode SelectedProtectionMode { get => _selectedProtectionMode; set => SetField(ref _selectedProtectionMode, value); }

    private bool _isExpiryEnabled;
    public bool IsExpiryEnabled { get => _isExpiryEnabled; set => SetField(ref _isExpiryEnabled, value); }

    private DateTime _expiryDate = DateTime.Today.AddYears(1);
    public DateTime ExpiryDate { get => _expiryDate; set => SetField(ref _expiryDate, value); }

    private bool _isMaxRunsEnabled;
    public bool IsMaxRunsEnabled { get => _isMaxRunsEnabled; set => SetField(ref _isMaxRunsEnabled, value); }

    private int _maxRunsValue = 3;
    public int MaxRunsValue { get => _maxRunsValue; set => SetField(ref _maxRunsValue, value); }

    // --- Build status ---
    private bool _isBuilding;
    public bool IsBuilding { get => _isBuilding; set => SetField(ref _isBuilding, value); }

    private double _buildProgress;
    public double BuildProgress { get => _buildProgress; set => SetField(ref _buildProgress, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    private Guid? _lastPackageId;
    public Guid? LastPackageId { get => _lastPackageId; set => SetField(ref _lastPackageId, value); }

    // --- Activation code generator (separate mini-tool for a already-built package) ---
    private string _actCodePackageId = string.Empty;
    public string ActCodePackageId { get => _actCodePackageId; set => SetField(ref _actCodePackageId, value); }

    private string _actCodeMachineId = string.Empty;
    public string ActCodeMachineId { get => _actCodeMachineId; set => SetField(ref _actCodeMachineId, value); }

    private bool _actCodeExpiryEnabled;
    public bool ActCodeExpiryEnabled { get => _actCodeExpiryEnabled; set => SetField(ref _actCodeExpiryEnabled, value); }

    private DateTime _actCodeExpiryDate = DateTime.Today.AddYears(1);
    public DateTime ActCodeExpiryDate { get => _actCodeExpiryDate; set => SetField(ref _actCodeExpiryDate, value); }

    private bool _actCodeMaxRunsEnabled;
    public bool ActCodeMaxRunsEnabled { get => _actCodeMaxRunsEnabled; set => SetField(ref _actCodeMaxRunsEnabled, value); }

    private int _actCodeMaxRunsValue = 1;
    public int ActCodeMaxRunsValue { get => _actCodeMaxRunsValue; set => SetField(ref _actCodeMaxRunsValue, value); }

    private string _actCodeResult = string.Empty;
    public string ActCodeResult { get => _actCodeResult; set => SetField(ref _actCodeResult, value); }

    public LocalizationManager Loc => LocalizationManager.Instance;

    public ICommand AddVideosCommand { get; }
    public ICommand RemoveVideoCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand BrowseLogoCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand BuildPackageCommand { get; }
    public ICommand GenerateActivationCodeCommand { get; }
    public ICommand CopyPackageIdCommand { get; }
    public ICommand UseForActivationCommand { get; }

    public MainViewModel()
    {
        VendorPublicKeyFingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(_vendorIdentity.PublicKey)).Substring(0, 16);

        foreach (var entry in PackageHistoryStore.Load())
            BuildHistory.Add(entry);

        AddVideosCommand = new RelayCommand(_ => AddVideos());
        RemoveVideoCommand = new RelayCommand(p => { if (p is VideoItemViewModel v) Videos.Remove(v); });
        MoveUpCommand = new RelayCommand(p => Move(p as VideoItemViewModel, -1));
        MoveDownCommand = new RelayCommand(p => Move(p as VideoItemViewModel, +1));
        BrowseLogoCommand = new RelayCommand(_ => BrowseLogo());
        ToggleLanguageCommand = new RelayCommand(_ =>
            Loc.CurrentLanguage = Loc.CurrentLanguage == AppLanguage.Arabic ? AppLanguage.English : AppLanguage.Arabic);
        BuildPackageCommand = new AsyncRelayCommand(_ => BuildPackageAsync(), _ => CanBuild());
        GenerateActivationCodeCommand = new RelayCommand(_ => GenerateActivationCode());
        CopyPackageIdCommand = new RelayCommand(p => { if (p is PackageHistoryEntry entry) Clipboard.SetText(entry.PackageId.ToString()); });
        UseForActivationCommand = new RelayCommand(p => { if (p is PackageHistoryEntry entry) ActCodePackageId = entry.PackageId.ToString(); });
    }

    private bool CanBuild() =>
        Videos.Count > 0 &&
        !string.IsNullOrEmpty(Password) &&
        Password == ConfirmPassword &&
        !IsBuilding;

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths.Where(IsVideoFile))
            Videos.Add(new VideoItemViewModel(path));
    }

    private static bool IsVideoFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp4" or ".mkv" or ".mov" or ".avi" or ".wmv" or ".m4v" or ".webm";
    }

    private void AddVideos()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Video files|*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.m4v;*.webm|All files|*.*",
        };
        if (dialog.ShowDialog() == true)
            AddFiles(dialog.FileNames);
    }

    private void Move(VideoItemViewModel? item, int delta)
    {
        if (item is null) return;
        int index = Videos.IndexOf(item);
        int newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= Videos.Count) return;
        Videos.Move(index, newIndex);
    }

    private void BrowseLogo()
    {
        var dialog = new OpenFileDialog { Filter = "PNG image|*.png" };
        if (dialog.ShowDialog() == true)
            LogoPath = dialog.FileName;
    }

    private async Task BuildPackageAsync()
    {
        var saveDialog = new SaveFileDialog { Filter = "Application (*.exe)|*.exe", FileName = $"{PlayerTitle}.exe" };
        if (saveDialog.ShowDialog() != true) return;

        IsBuilding = true;
        BuildProgress = 0;
        StatusMessage = Loc["building"];

        try
        {
            var packageId = Guid.NewGuid();

            byte[] logoBytes = !string.IsNullOrEmpty(LogoPath) && File.Exists(LogoPath)
                ? await File.ReadAllBytesAsync(LogoPath)
                : Array.Empty<byte>();

            var branding = new BrandingInfo
            {
                Title = PlayerTitle,
                Theme = SelectedTheme,
                AccentColorHex = AccentColorHex,
                LogoPng = logoBytes,
            };

            var videos = Videos.Select(v => new VideoSourceItem { Id = v.Id, Title = v.Title, FilePath = v.FilePath }).ToList();

            bool requireActivation = SelectedProtectionMode == ProtectionMode.ActivationCode;

            var request = new ContainerBuildRequest
            {
                PackageId = packageId,
                Password = Password,
                RequireActivationCode = requireActivation,
                ExpiresUtc = IsExpiryEnabled ? ExpiryDate.ToUniversalTime() : null,
                MaxRuns = IsMaxRunsEnabled ? MaxRunsValue : null,
                Branding = branding,
                Videos = videos,
            };

            var progress = new Progress<double>(p => BuildProgress = p * 100.0);
            await _packageBuilder.BuildAsync(request, _vendorIdentity, saveDialog.FileName, progress);

            LastPackageId = packageId;
            ActCodePackageId = packageId.ToString();

            var historyEntry = new PackageHistoryEntry
            {
                PackageId = packageId,
                Title = PlayerTitle,
                BuiltUtc = DateTime.UtcNow,
                OutputPath = saveDialog.FileName,
                RequiresActivationCode = requireActivation,
            };
            PackageHistoryStore.Append(historyEntry);
            BuildHistory.Insert(0, historyEntry);

            StatusMessage = $"{Loc["build_success"]}: {saveDialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, Loc["app_name"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private void GenerateActivationCode()
    {
        try
        {
            if (!Guid.TryParse(ActCodePackageId, out var packageId))
                throw new FormatException("Package ID is not a valid GUID.");

            var token = new LicenseToken
            {
                PackageId = packageId,
                MachineId = ActCodeMachineId.Trim(),
                ExpiresUtc = ActCodeExpiryEnabled ? ActCodeExpiryDate.ToUniversalTime() : null,
                MaxRuns = ActCodeMaxRunsEnabled ? ActCodeMaxRunsValue : null,
            };

            ActCodeResult = ActivationCodeCodec.Encode(token, _vendorIdentity);
        }
        catch (Exception ex)
        {
            ActCodeResult = string.Empty;
            MessageBox.Show(ex.Message, Loc["app_name"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
