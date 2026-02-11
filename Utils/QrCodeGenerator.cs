using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace TwoFactorAuth.Utils;

public static class QrCodeGenerator
{
    public static Bitmap Generate(string content, int width = 256, int height = 256)
    {
        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 1
            }
        };

        using var skBitmap = writer.Write(content);
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();

        data.SaveTo(stream);
        stream.Position = 0;

        return new Bitmap(stream);
    }
}
