namespace MajorMode.Core.Model;

public enum FretboardOrientation
{
    /// <summary>Слева 6 струна, справа 1; лады растут сверху вниз.</summary>
    Vertical,

    /// <summary>Снизу 6 струна, сверху 1; лады растут слева направо.</summary>
    Horizontal,
}

public enum CanvasFormat
{
    Landscape16x9,
    Portrait9x16,
    Landscape16x9_4K,
    Portrait9x16_4K,
}

public enum ImageStyle
{
    Schematic,
    ModernDark,
    RealisticWood,
}

public enum ExportFormat
{
    Gif,
    Png,
    Jpg,
}

public readonly record struct CanvasSize(int Width, int Height);

public static class RenderOptionExtensions
{
    public static CanvasSize ToSize(this CanvasFormat format) => format switch
    {
        CanvasFormat.Landscape16x9 => new CanvasSize(1920, 1080),
        CanvasFormat.Portrait9x16 => new CanvasSize(1080, 1920),
        CanvasFormat.Landscape16x9_4K => new CanvasSize(3840, 2160),
        CanvasFormat.Portrait9x16_4K => new CanvasSize(2160, 3840),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static string DisplayName(this CanvasFormat format) => format switch
    {
        CanvasFormat.Landscape16x9 => "16:9 альбомный, 1920 на 1080",
        CanvasFormat.Portrait9x16 => "9:16 портретный, 1080 на 1920",
        CanvasFormat.Landscape16x9_4K => "16:9 альбомный 4K, 3840 на 2160",
        CanvasFormat.Portrait9x16_4K => "9:16 портретный 4K, 2160 на 3840",
        _ => format.ToString(),
    };

    public static string DisplayName(this ImageStyle style) => style switch
    {
        ImageStyle.Schematic => "Схематичный: черный фон, белые линии",
        ImageStyle.ModernDark => "Современный темный",
        ImageStyle.RealisticWood => "Реалистичный деревянный гриф",
        _ => style.ToString(),
    };

    public static string DisplayName(this ExportFormat format) => format switch
    {
        ExportFormat.Gif => "GIF (основной)",
        ExportFormat.Png => "PNG (без потерь)",
        ExportFormat.Jpg => "JPG",
        _ => format.ToString(),
    };

    public static string FileExtension(this ExportFormat format) => format switch
    {
        ExportFormat.Gif => ".gif",
        ExportFormat.Png => ".png",
        ExportFormat.Jpg => ".jpg",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}

/// <summary>Параметры картинки, не влияющие на аппликатуру.</summary>
public sealed record RenderOptions(
    FretboardOrientation Orientation = FretboardOrientation.Vertical,
    CanvasFormat Canvas = CanvasFormat.Landscape16x9,
    ImageStyle Style = ImageStyle.Schematic);
