using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace TwoFactorAuth.Converters;

public class IconColorConverter : IValueConverter
{
    private const byte DarkThreshold = 50;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                var color = Color.Parse(hex);

                if (IsDarkMode() && color.R <= DarkThreshold && color.G <= DarkThreshold && color.B <= DarkThreshold)
                {
                    color = Colors.White;
                }

                return new SolidColorBrush(color);
            }
            catch
            {
            
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static bool IsDarkMode()
    {
        var app = Application.Current;
        if (app == null) return false;
        return app.ActualThemeVariant == ThemeVariant.Dark;
    }
}
