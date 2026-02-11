using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace TwoFactorAuth.Converters;

public class MultiSelectButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMultiSelectMode)
        {
            var key = isMultiSelectMode 
                ? "Lang.Category.CancelMultiSelect" 
                : "Lang.Category.MultiSelect";
            
            if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
            {
                return s;
            }
            return key;
        }
        return "Multi-Select";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
