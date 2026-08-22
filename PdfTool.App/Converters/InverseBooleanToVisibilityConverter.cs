using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PdfTool.App.Converters;

/// <summary>Collapses an element when the bound flag is true (WPF only ships the opposite).</summary>
internal sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
