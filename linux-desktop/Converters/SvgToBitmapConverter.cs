using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using TwoFactorAuth.Models;
using TwoFactorAuth.Utils;

namespace TwoFactorAuth.Converters;

public class SvgToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ServiceProvider sp && !string.IsNullOrEmpty(sp.IconPath))
        {
            var svgContent = SvgImageHelper.IsFullSvg(sp.IconPath)
                ? sp.IconPath
                : SvgImageHelper.WrapPathDataAsSvg(sp.IconPath, sp.IconColor);
            return SvgImageHelper.FromSvgString(svgContent, 32, 32);
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
