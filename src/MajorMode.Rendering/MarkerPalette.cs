using SkiaSharp;

namespace MajorMode.Rendering;

/// <summary>
/// Цвета маркеров общие для всех стилей. Цвет не единственный носитель смысла: в кружке всегда цифра пальца.
/// </summary>
public static class MarkerPalette
{
    public static readonly SKColor Finger1Red = new(0xD3, 0x2F, 0x2F);
    public static readonly SKColor Finger2Orange = new(0xF5, 0x7C, 0x00);
    public static readonly SKColor Finger3Yellow = new(0xFD, 0xD8, 0x35);
    public static readonly SKColor Finger4Green = new(0x2E, 0x7D, 0x32);
    public static readonly SKColor MuteRed = new(0xFF, 0x45, 0x3A);
    public static readonly SKColor DarkDigit = new(0x11, 0x11, 0x11);
    public static readonly SKColor LightDigit = new(0xFF, 0xFF, 0xFF);

    public static SKColor FingerColor(int finger) => finger switch
    {
        1 => Finger1Red,
        2 => Finger2Orange,
        3 => Finger3Yellow,
        4 => Finger4Green,
        _ => throw new ArgumentOutOfRangeException(nameof(finger)),
    };

    /// <summary>Цвет цифры с максимальным контрастом к фону кружка.</summary>
    public static SKColor DigitColor(SKColor background) =>
        ContrastRatio(background, DarkDigit) >= ContrastRatio(background, LightDigit) ? DarkDigit : LightDigit;

    /// <summary>Контраст по WCAG 2.x.</summary>
    public static double ContrastRatio(SKColor a, SKColor b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    public static double RelativeLuminance(SKColor c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.Red) + 0.7152 * Channel(c.Green) + 0.0722 * Channel(c.Blue);
    }
}
