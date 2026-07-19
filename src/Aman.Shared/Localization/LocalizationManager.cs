using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Aman.Shared.Localization;

public enum AppLanguage { Arabic, English }

/// <summary>
/// Minimal, dependency-free i18n: a static string table plus an
/// INotifyPropertyChanged indexer so XAML can bind with
/// <c>{Binding Source={x:Static loc:LocalizationManager.Instance}, Path=[key]}</c>
/// and refresh live when <see cref="CurrentLanguage"/> changes.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private AppLanguage _language = AppLanguage.Arabic;
    public AppLanguage CurrentLanguage
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); // empty => refresh everything, incl. the indexer
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
        }
    }

    public FlowDirection FlowDirection => _language == AppLanguage.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string this[string key] => Strings.Table.TryGetValue(key, out var byLang)
        ? byLang.GetValueOrDefault(_language, key)
        : key;
}
