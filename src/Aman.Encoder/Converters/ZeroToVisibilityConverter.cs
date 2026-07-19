using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Aman.Encoder.Converters;

/// <summary>Visible when the bound int is 0 — used to show an empty-state hint (e.g. "no packages exported yet").</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
