using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using ZXing;

namespace TwoFactorAuth.Utils;

public static class QrCodeDecoder
{
    public static IReadOnlyList<string> DecodeQrTexts(Bitmap bitmap)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        if (width <= 0 || height <= 0) return Array.Empty<string>();

        var stride = checked(width * 4);
        var buffer = new byte[checked(stride * height)];

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), buffer.Length, stride);

            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
                }
            };

            var results = reader.DecodeMultiple(buffer, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
            if (results == null || results.Length == 0) return Array.Empty<string>();

            return results
                .Select(r => r.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            handle.Free();
        }
    }
}
