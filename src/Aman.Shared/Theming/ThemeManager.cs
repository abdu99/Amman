using System;
using System.Linq;
using System.Windows;

namespace Aman.Shared.Theming;

/// <summary>Swaps the "Colors.*" resource dictionary in-place so open windows re-skin live.</summary>
public static class ThemeManager
{
    public static void Apply(Uri colorsDictionaryUri)
    {
        var app = Application.Current;
        if (app is null) return;

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("/Themes/Colors."));

        var newDict = new ResourceDictionary { Source = colorsDictionaryUri };

        if (existing is not null)
            app.Resources.MergedDictionaries[app.Resources.MergedDictionaries.IndexOf(existing)] = newDict;
        else
            app.Resources.MergedDictionaries.Insert(0, newDict);
    }
}
