using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MailIntegrator.Converters;

/// <summary>
/// Converts a boolean to <see cref="Visibility"/> using the inverse rule, so
/// <see langword="true"/> collapses and <see langword="false"/> shows.
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}
