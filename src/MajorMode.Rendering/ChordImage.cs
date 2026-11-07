using SkiaSharp;

namespace MajorMode.Rendering;

/// <summary>Готовая картинка аккорда (BGRA, непрозрачная). Один экземпляр и для превью, и для экспорта.</summary>
public sealed class ChordImage : IDisposable
{
    internal ChordImage(SKBitmap bitmap)
    {
        Bitmap = bitmap;
    }

    public SKBitmap Bitmap { get; }

    public int Width => Bitmap.Width;

    public int Height => Bitmap.Height;

    /// <summary>Пиксели BGRA построчно, шаг строки = Width * 4.</summary>
    public byte[] GetBgraPixels()
    {
        var info = Bitmap.Info;
        if (info.ColorType != SKColorType.Bgra8888 || info.RowBytes != Width * 4)
        {
            throw new InvalidOperationException("Ожидается BGRA8888 без выравнивания строк.");
        }

        return Bitmap.GetPixelSpan().ToArray();
    }

    public void Dispose() => Bitmap.Dispose();
}
