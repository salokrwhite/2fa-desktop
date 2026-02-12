using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Svg.Skia;
using SkiaSharp;

namespace TwoFactorAuth.Utils;
public static class SvgImageHelper
{
    private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    public static Bitmap? FromSvgString(string? svgContent, int width = 64, int height = 64, bool adaptDarkMode = true)
    {
        if (string.IsNullOrWhiteSpace(svgContent))
            return null;

        var isDark = adaptDarkMode && IsDarkMode();
        var cacheKey = $"{svgContent.GetHashCode()}_{width}_{height}_{isDark}";

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var svg = svgContent;
            if (isDark)
                svg = AdaptForDarkMode(svg);

            using var skSvg = new SKSvg();
            skSvg.FromSvg(svg);

            if (skSvg.Picture == null)
                return null;

            var bounds = skSvg.Picture.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            var scaleX = width / bounds.Width;
            var scaleY = height / bounds.Height;
            var scale = Math.Min(scaleX, scaleY);

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null) return null;

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            var scaledW = bounds.Width * scale;
            var scaledH = bounds.Height * scale;
            canvas.Translate((width - scaledW) / 2f, (height - scaledH) / 2f);
            canvas.Scale((float)scale);
            canvas.Translate(-bounds.Left, -bounds.Top);

            canvas.DrawPicture(skSvg.Picture);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            var stream = new System.IO.MemoryStream(data.ToArray());
            var bitmap = new Bitmap(stream);

            _cache.TryAdd(cacheKey, bitmap);
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SvgImageHelper] Render failed: {ex.Message}");
            return null;
        }
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }

    public static string WrapPathDataAsSvg(string pathData, string? color = null)
    {
        var fill = string.IsNullOrEmpty(color) ? "#000000" : color;
        return $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="{pathData}" fill="{fill}"/></svg>""";
    }

    public static bool IsFullSvg(string? content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<!--", StringComparison.Ordinal) && trimmed.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string AdaptForDarkMode(string svg)
    {
        svg = svg.Replace("fill=\"#000000\"", "fill=\"#FFFFFF\"");
        svg = svg.Replace("fill=\"#000\"", "fill=\"#FFFFFF\"");
        svg = svg.Replace("fill=\"black\"", "fill=\"#FFFFFF\"");
        svg = svg.Replace("fill=\"#181717\"", "fill=\"#FFFFFF\""); 
        svg = svg.Replace("fill=\"#191C1F\"", "fill=\"#FFFFFF\""); 
        svg = svg.Replace("fill:#000000", "fill:#FFFFFF");
        svg = svg.Replace("fill:#000", "fill:#FFFFFF");
        svg = svg.Replace("fill:black", "fill:#FFFFFF");
        svg = svg.Replace("fill:#181717", "fill:#FFFFFF");
        svg = svg.Replace("fill:#191C1F", "fill:#FFFFFF");
        return svg;
    }

    private static bool IsDarkMode()
    {
        var app = Application.Current;
        return app?.ActualThemeVariant == ThemeVariant.Dark;
    }
}
