using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace TwoFactorAuth.Converters;

public class DynamicResourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string resourceKey && Application.Current != null)
        {
            if (Application.Current.TryGetResource(resourceKey, null, out var resource))
            {
                return resource;
            }
            return resourceKey; 
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}