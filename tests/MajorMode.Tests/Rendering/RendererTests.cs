using MajorMode.Core;
using MajorMode.Core.Layout;
using MajorMode.Core.Model;
using MajorMode.Core.Parsing;
using MajorMode.Rendering;
using SkiaSharp;

namespace MajorMode.Tests.Rendering;

public class RendererTests
{
    internal static ChordImage Render(ChordInput input, RenderOptions options)
    {
        var a = ChordAnalyzer.Analyze(input);
        Assert.True(a.CanRender, ChordAnalyzer.BuildErrorSummary(a.Messages));
        return ChordRenderer.Render(a.Chord!, a.Range!.Value, options);
    }

    private static ChordLayout LayoutFor(ChordInput input, RenderOptions options)
    {
        var a = ChordAnalyzer.Analyze(input);
        return ChordLayout.Create(options.Canvas.ToSize(), options.Orientation, a.Range!.Value);
    }

    private static SKColor Pixel(ChordImage image, double x, double y) => image.Bitmap.GetPixel((int)Math.Round(x), (int)Math.Round(y));

    public static TheoryData<ImageStyle, FretboardOrientation, CanvasFormat> AllCombinations()
    {
        var data = new TheoryData<ImageStyle, FretboardOrientation, CanvasFormat>();
        foreach (var s in Enum.GetValues<ImageStyle>())
        {
            foreach (var o in Enum.GetValues<FretboardOrientation>())
            {
                foreach (var c in new[] { CanvasFormat.Landscape16x9, CanvasFormat.Portrait9x16 })
                {
                    data.Add(s, o, c);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Render_IsDeterministic_AndHasCanvasSize(ImageStyle style, FretboardOrientation orientation, CanvasFormat canvas)
    {
        var options = new RenderOptions(orientation, canvas, style);
        using var a = Render(TestChords.F, options);
        using var b = Render(TestChords.F, options);

        Assert.Equal(canvas.ToSize().Width, a.Width);
        Assert.Equal(canvas.ToSize().Height, a.Height);
        Assert.True(a.GetBgraPixels().AsSpan().SequenceEqual(b.GetBgraPixels()));
    }

    [Fact]
    public void Render_4K_Works()
    {
        using var image = Render(TestChords.C, new RenderOptions(FretboardOrientation.Horizontal, CanvasFormat.Portrait9x16_4K, ImageStyle.RealisticWood));
        Assert.Equal(2160, image.Width);
        Assert.Equal(3840, image.Height);
    }

    [Fact]
    public void DifferentChords_GiveDifferentPixels()
    {
        var options = new RenderOptions();
        using var c = Render(TestChords.C, options);
        using var f = Render(TestChords.F, options);
        Assert.False(c.GetBgraPixels().AsSpan().SequenceEqual(f.GetBgraPixels()));
    }

    [Theory]
    [InlineData(FretboardOrientation.Vertical)]
    [InlineData(FretboardOrientation.Horizontal)]
    public void FingerMarkers_AreDrawnAtLayoutPositions_WithFingerColors(FretboardOrientation orientation)
    {
        foreach (var style in Enum.GetValues<ImageStyle>())
        {
            var options = new RenderOptions(orientation, CanvasFormat.Landscape16x9, style);
            using var image = Render(TestChords.C, options);
            var layout = LayoutFor(TestChords.C, options);

            AssertMarker(image, layout, 2, 1, MarkerPalette.Finger1Red);
            AssertMarker(image, layout, 4, 2, MarkerPalette.Finger2Orange);
            AssertMarker(image, layout, 5, 3, MarkerPalette.Finger3Yellow);
        }
    }

    [Fact]
    public void MutedString_ShowsRedX_OpenString_ShowsRing()
    {
        var options = new RenderOptions(FretboardOrientation.Vertical, CanvasFormat.Landscape16x9, ImageStyle.Schematic);
        using var image = Render(TestChords.C, options);
        var layout = LayoutFor(TestChords.C, options);

        var x = layout.IndicatorCenter(6);
        Assert.Equal(MarkerPalette.MuteRed, Pixel(image, x.X, x.Y));

        var o = layout.IndicatorCenter(1);
        Assert.Equal(SKColors.Black, Pixel(image, o.X, o.Y));
        Assert.Equal(SKColors.White, Pixel(image, o.X + layout.IndicatorRadius * 0.9, o.Y));

        var fretted = layout.IndicatorCenter(2);
        Assert.Equal(SKColors.Black, Pixel(image, fretted.X, fretted.Y));
        Assert.Equal(SKColors.Black, Pixel(image, fretted.X + layout.IndicatorRadius * 0.9, fretted.Y));
    }

    [Theory]
    [InlineData(FretboardOrientation.Vertical)]
    [InlineData(FretboardOrientation.Horizontal)]
    public void Barre_IsRedBarAcrossAllStrings(FretboardOrientation orientation)
    {
        var options = new RenderOptions(orientation, CanvasFormat.Landscape16x9, ImageStyle.ModernDark);
        using var image = Render(TestChords.Barre5, options);
        var layout = LayoutFor(TestChords.Barre5, options);

        Assert.Equal(5, layout.Range.Start);
        foreach (var s in new[] { 6, 5, 1 })
        {
            var c = layout.CellCenter(s, 5);
            Assert.Equal(MarkerPalette.Finger1Red, Pixel(image, c.X, c.Y));
        }
    }

    [Fact]
    public void FingerOnMutedString_IsNotDrawn_XIsShown()
    {
        var input = ChordInput.Create("X", "5", new FingerInput("3", "5"));
        var options = new RenderOptions(FretboardOrientation.Vertical, CanvasFormat.Landscape16x9, ImageStyle.Schematic);
        using var image = Render(input, options);
        var layout = LayoutFor(input, options);

        var c = layout.CellCenter(5, 3);
        Assert.NotEqual(MarkerPalette.Finger1Red, Pixel(image, c.X + layout.MarkerRadius * 0.7, c.Y));
        var x = layout.IndicatorCenter(5);
        Assert.Equal(MarkerPalette.MuteRed, Pixel(image, x.X, x.Y));
    }

    [Fact]
    public void Image_IsFullyOpaque()
    {
        using var image = Render(TestChords.F, new RenderOptions(Style: ImageStyle.RealisticWood));
        var px = image.GetBgraPixels();
        for (var i = 3; i < px.Length; i += 4)
        {
            Assert.Equal(255, px[i]);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DigitColor_HasWcagAaContrastWithMarker(int finger)
    {
        var bg = MarkerPalette.FingerColor(finger);
        Assert.True(MarkerPalette.ContrastRatio(bg, MarkerPalette.DigitColor(bg)) >= 4.5);
    }

    [Fact]
    public void FingerColors_AreDistinct()
    {
        var colors = Enumerable.Range(1, 4).Select(MarkerPalette.FingerColor).ToHashSet();
        Assert.Equal(4, colors.Count);
    }

    private static void AssertMarker(ChordImage image, ChordLayout layout, int guitarString, int fret, SKColor expected)
    {
        var c = layout.CellCenter(guitarString, fret);
        // Сбоку от цифры, внутри кружка.
        var probe = Pixel(image, c.X + layout.MarkerRadius * 0.75, c.Y);
        Assert.Equal(expected, probe);
    }
}
