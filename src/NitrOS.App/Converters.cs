using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace NitrOS.App;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is not null and not "" ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        value is not Visibility.Visible;
}

public sealed class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        value is not true;
}

public sealed class HasItemsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is System.Collections.ICollection c) return c.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is int count) return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class NoItemsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language) =>
        value is System.Collections.ICollection { Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Chip colors for a switch's applied state: green applied, yellow not applied, neutral otherwise.
/// ConverterParameter "bg" gives the background, anything else the text color.</summary>
public sealed class AppliedStateBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var background = parameter as string == "bg";
        var key = (value is NitrOS.App.Logic.Screens.AppliedState state ? state : default, background) switch
        {
            (NitrOS.App.Logic.Screens.AppliedState.Applied, true) => "SystemFillColorSuccessBackgroundBrush",
            (NitrOS.App.Logic.Screens.AppliedState.Applied, false) => "SystemFillColorSuccessBrush",
            (NitrOS.App.Logic.Screens.AppliedState.NotApplied, true) => "SystemFillColorCautionBackgroundBrush",
            (NitrOS.App.Logic.Screens.AppliedState.NotApplied, false) => "SystemFillColorCautionBrush",
            (_, true) => "CockpitRaisedBrush",
            _ => "CockpitTextSecondary",
        };
        return Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}
