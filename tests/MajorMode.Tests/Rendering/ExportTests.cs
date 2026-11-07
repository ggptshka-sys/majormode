using MajorMode.Core.Files;
using MajorMode.Core.Model;
using MajorMode.Rendering;
using MajorMode.Rendering.Export;
using SkiaSharp;

namespace MajorMode.Tests.Rendering;

public sealed class ExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "majormode-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public ExportTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(CanvasFormat.Portrait9x16, ImageStyle.Schematic, FretboardOrientation.Vertical)]
    [InlineData(CanvasFormat.Landscape16x9, ImageStyle.Schematic, FretboardOrientation.Horizontal)]
    [InlineData(CanvasFormat.Portrait9x16, ImageStyle.ModernDark, FretboardOrientation.Horizontal)]
    [InlineData(CanvasFormat.Landscape16x9, ImageStyle.RealisticWood, FretboardOrientation.Vertical)]
    [InlineData(CanvasFormat.Portrait9x16, ImageStyle.RealisticWood, FretboardOrientation.Horizontal)]
    public void Gif_IsRealStaticGif_AndCloseToSource(CanvasFormat canvas, ImageStyle style, FretboardOrientation orientation)
    {
        using var image = RendererTests.Render(TestChords.F, new RenderOptions(orientation, canvas, style));
        var bytes = ImageExporter.Encode(image, ExportFormat.Gif, style);

        Assert.Equal("GIF89a"u8.ToArray(), bytes[..6]);
        Assert.Equal(0x3B, bytes[^1]);

        var decoded = TestGifDecoder.Decode(bytes);
        Assert.Equal(1, decoded.FrameCount);
        Assert.Equal(image.Width, decoded.Width);
        Assert.Equal(image.Height, decoded.Height);

        var src = image.GetBgraPixels();
        long totalError = 0;
        var exact = 0;
        for (var i = 0; i < decoded.Rgb.Length; i++)
        {
            var rgb = decoded.Rgb[i];
            var o = i * 4;
            var d = Math.Abs(((rgb >> 16) & 0xFF) - src[o + 2]) + Math.Abs(((rgb >> 8) & 0xFF) - src[o + 1]) + Math.Abs((rgb & 0xFF) - src[o]);
            totalError += d;
            if (d == 0)
            {
                exact++;
            }
        }

        var meanError = totalError / (double)decoded.Rgb.Length;
        var exactShare = exact / (double)decoded.Rgb.Length;
        _output.WriteLine($"{style} {orientation} {canvas}: средняя ошибка {meanError:F3}, точных пикселей {exactShare:P2}, {bytes.Length / 1024} КБ");
        Assert.True(meanError < 6, $"Средняя ошибка {meanError:F2}");
        if (style == ImageStyle.Schematic)
        {
            Assert.True(meanError < 2, $"Схематичный стиль: средняя ошибка {meanError:F2}");
            Assert.True(exactShare > 0.9, $"Точных пикселей {exactShare:P1}");
        }

        // Плоские цвета маркеров попадают в палитру точно.
        var palette = decoded.Rgb.ToHashSet();
        Assert.Contains(Rgb(MarkerPalette.Finger1Red), palette);
        Assert.Contains(Rgb(MarkerPalette.Finger2Orange), palette);
        Assert.Contains(Rgb(MarkerPalette.Finger3Yellow), palette);
        Assert.Contains(Rgb(MarkerPalette.Finger4Green), palette);
    }

    [Fact]
    public void GifEncoder_RoundTripsSmallExactImage()
    {
        const int w = 37, h = 23;
        var px = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++)
        {
            var k = i * 31 % 200;
            px[i * 4] = (byte)(k * 7 % 256);
            px[i * 4 + 1] = (byte)(k % 5 * 40);
            px[i * 4 + 2] = (byte)(k % 3 * 100);
            px[i * 4 + 3] = 255;
        }

        using var ms = new MemoryStream();
        GifEncoder.Encode(px, w, h, ms);
        var decoded = TestGifDecoder.Decode(ms.ToArray());

        var distinct = Enumerable.Range(0, w * h).Select(i => (px[i * 4 + 2] << 16) | (px[i * 4 + 1] << 8) | px[i * 4]).Distinct().Count();
        Assert.True(distinct <= 256);
        for (var i = 0; i < w * h; i++)
        {
            Assert.Equal((px[i * 4 + 2] << 16) | (px[i * 4 + 1] << 8) | px[i * 4], decoded.Rgb[i]);
        }
    }

    [Fact]
    public void GifEncoder_HandlesDictionaryResetOnNoisyImage()
    {
        const int w = 300, h = 200;
        var px = new byte[w * h * 4];
        uint state = 1;
        for (var i = 0; i < w * h; i++)
        {
            state = state * 1664525u + 1013904223u;
            var v = (byte)(state >> 24);
            px[i * 4] = v;
            px[i * 4 + 1] = (byte)(v ^ 0x5A);
            px[i * 4 + 2] = (byte)(v * 3);
            px[i * 4 + 3] = 255;
        }

        using var ms = new MemoryStream();
        GifEncoder.Encode(px, w, h, ms, dither: GifDither.Never);
        var decoded = TestGifDecoder.Decode(ms.ToArray());
        Assert.Equal(w * h, decoded.Rgb.Length);
    }

    [Fact]
    public void Png_IsLossless()
    {
        using var image = RendererTests.Render(TestChords.C, new RenderOptions(Style: ImageStyle.RealisticWood));
        var bytes = ImageExporter.Encode(image, ExportFormat.Png, ImageStyle.RealisticWood);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);
        using var decoded = SKBitmap.Decode(bytes).Copy(SKColorType.Bgra8888);
        Assert.True(decoded.GetPixelSpan().SequenceEqual(image.GetBgraPixels()));
    }

    [Fact]
    public void Jpg_IsJpegOpaqueAndSameSize()
    {
        using var image = RendererTests.Render(TestChords.C, new RenderOptions(FretboardOrientation.Horizontal, CanvasFormat.Portrait9x16));
        var bytes = ImageExporter.Encode(image, ExportFormat.Jpg, ImageStyle.Schematic);

        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, bytes[..3]);
        using var decoded = SKBitmap.Decode(bytes);
        Assert.Equal(1080, decoded.Width);
        Assert.Equal(1920, decoded.Height);
        Assert.Equal(SKAlphaType.Opaque, decoded.AlphaType);
    }

    [Fact]
    public void Save_WritesFile_RefusesSilentOverwrite_AndOverwritesWhenConfirmed()
    {
        using var image = RendererTests.Render(TestChords.C, new RenderOptions());
        var (path, error) = FileNameSanitizer.BuildFullPath(_dir, "C#", ExportFormat.Gif);
        Assert.Null(error);

        var first = ImageExporter.Save(image, ExportFormat.Gif, ImageStyle.Schematic, path!, overwrite: false);
        Assert.True(first.Success, first.Message);
        Assert.StartsWith("Сохранено: ", first.Message);
        Assert.True(File.Exists(path));
        var original = File.ReadAllBytes(path!);

        using var other = RendererTests.Render(TestChords.F, new RenderOptions());
        var refused = ImageExporter.Save(other, ExportFormat.Gif, ImageStyle.Schematic, path!, overwrite: false);
        Assert.False(refused.Success);
        Assert.Equal(original, File.ReadAllBytes(path!));

        var replaced = ImageExporter.Save(other, ExportFormat.Gif, ImageStyle.Schematic, path!, overwrite: true);
        Assert.True(replaced.Success);
        Assert.NotEqual(original, File.ReadAllBytes(path!));

        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Save_ToMissingFolder_ReturnsReadableError()
    {
        using var image = RendererTests.Render(TestChords.C, new RenderOptions());
        var result = ImageExporter.Save(image, ExportFormat.Png, ImageStyle.Schematic, Path.Combine(_dir, "nope", "C.png"), overwrite: false);

        Assert.False(result.Success);
        Assert.Contains("Папка не найдена", result.Message);
    }

    private static int Rgb(SKColor c) => (c.Red << 16) | (c.Green << 8) | c.Blue;
}
