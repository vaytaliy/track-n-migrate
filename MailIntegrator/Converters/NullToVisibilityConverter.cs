using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MailIntegrator.Converters;

/// <summary>
/// Shows an element when the bound value exists and collapses it when the value is
/// <see langword="null"/>, so an optional affordance such as the tracking link can be hidden without an
/// extra flag on the view model.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
