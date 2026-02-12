using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace TwoFactorAuth.Converters;

public class SelectButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelectionMode)
        {
            var key = isSelectionMode 
                ? "Lang.CancelSelect" 
                : "Lang.Select";
            
            if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
            {
                return s;
            }
            return key;
        }
        return "Select";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
