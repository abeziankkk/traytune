using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrayTune;

/// <summary>
/// Главный цвет обложки для режима «Из обложки». Берём уменьшенную копию, отбрасываем
/// почти белое, почти чёрное и серое, считаем доминирующий оттенок и подтягиваем его
/// до яркости и насыщенности, при которых он читается на тёмной панели задач.
/// </summary>
internal static class CoverPalette
{
    private const int Size = 16;
    private const int HueBuckets = 12;

    public static Brush? From(BitmapSource? art)
    {
        try
        {
            if (art is null || art.PixelWidth <= 0 || art.PixelHeight <= 0) return null;

            var scaled = new TransformedBitmap(art, new ScaleTransform(
                (double)Size / art.PixelWidth,
                (double)Size / art.PixelHeight));
            var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);

            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);

            var weight = new double[HueBuckets];
            var sumR = new double[HueBuckets];
            var sumG = new double[HueBuckets];
            var sumB = new double[HueBuckets];

            for (var i = 0; i + 3 < pixels.Length; i += 4)
            {
                var b = pixels[i] / 255.0;
                var g = pixels[i + 1] / 255.0;
                var r = pixels[i + 2] / 255.0;
                var a = pixels[i + 3] / 255.0;

                if (a < 0.7) continue;

                var (h, s, v) = RgbToHsv(r, g, b);

                // Белый, чёрный и серый фон обложки цветом не считаем.
                if (v < 0.12 || v > 0.92 || s < 0.15) continue;

                var bucket = (int)(h / 360.0 * HueBuckets) % HueBuckets;
                var w = s * v;
                weight[bucket] += w;
                sumR[bucket] += r * w;
                sumG[bucket] += g * w;
                sumB[bucket] += b * w;
            }

            var best = -1;
            var bestWeight = 0.0;
            for (var i = 0; i < HueBuckets; i++)
            {
                if (weight[i] <= bestWeight) continue;
                bestWeight = weight[i];
                best = i;
            }

            if (best < 0 || bestWeight <= 0) return null;

            var color = Color.FromRgb(
                (byte)Math.Clamp(sumR[best] / bestWeight * 255, 0, 255),
                (byte)Math.Clamp(sumG[best] / bestWeight * 255, 0, 255),
                (byte)Math.Clamp(sumB[best] / bestWeight * 255, 0, 255));

            var (hue, sat, val) = RgbToHsv(color.R / 255.0, color.G / 255.0, color.B / 255.0);

            // На панели задач фон тёмный: слишком тёмный или блёклый цвет не читается.
            val = Math.Max(val, 0.72);
            sat = Math.Clamp(sat, 0.5, 0.92);

            var brush = new SolidColorBrush(HsvToRgb(hue, sat, val));
            brush.Freeze();
            return brush;
        }
        catch
        {
            // Обложки бывают битые и «в процессе» — это не повод падать.
            return null;
        }
    }

    private static (double h, double s, double v) RgbToHsv(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hue = 0.0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * (((g - b) / delta) % 6);
            else if (max == g) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
        }

        if (hue < 0) hue += 360;
        var sat = max <= 0 ? 0 : delta / max;
        return (hue, sat, max);
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;

        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }
}
