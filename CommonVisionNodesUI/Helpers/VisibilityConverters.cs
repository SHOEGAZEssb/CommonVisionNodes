using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Returns <see cref="Visibility.Collapsed"/> when the value is <c>null</c>;
/// otherwise <see cref="Visibility.Visible"/>.
/// </summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns <see cref="Visibility.Collapsed"/> when the string value is null or empty;
/// otherwise <see cref="Visibility.Visible"/>.
/// </summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is string s && s.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}
