using MajorMode.Core.Layout;
using MajorMode.Core.Model;

namespace MajorMode.Tests.Core;

public class LayoutTests
{
    public static TheoryData<FretboardOrientation, CanvasFormat> AllCombinations()
    {
        var data = new TheoryData<FretboardOrientation, CanvasFormat>();
        foreach (var o in Enum.GetValues<FretboardOrientation>())
        {
            foreach (var c in Enum.GetValues<CanvasFormat>())
            {
                data.Add(o, c);
            }
        }

        return data;
    }

    [Fact]
    public void Vertical_String6IsLeft_String1IsRight_FretsGrowDown()
    {
        var layout = ChordLayout.Create(CanvasFormat.Landscape16x9.ToSize(), FretboardOrientation.Vertical, new FretRange(1, 5));

        for (var s = 6; s > 1; s--)
        {
            Assert.True(layout.StringOffset(s) < layout.StringOffset(s - 1));
        }

        Assert.True(layout.CellCenter(3, 1).Y < layout.CellCenter(3, 2).Y);
        Assert.Equal(layout.CellCenter(3, 1).X, layout.CellCenter(3, 4).X, 6);
        Assert.True(layout.IndicatorCenter(6).Y < layout.BoardRect.Y);
    }

    [Fact]
    public void Horizontal_String6IsBottom_String1IsTop_FretsGrowRight()
    {
        var layout = ChordLayout.Create(CanvasFormat.Landscape16x9.ToSize(), FretboardOrientation.Horizontal, new FretRange(1, 5));

        for (var s = 6; s > 1; s--)
        {
            Assert.True(layout.StringOffset(s) > layout.StringOffset(s - 1));
        }

        Assert.True(layout.CellCenter(3, 1).X < layout.CellCenter(3, 2).X);
        Assert.Equal(layout.CellCenter(3, 1).Y, layout.CellCenter(3, 4).Y, 6);
        Assert.True(layout.IndicatorCenter(6).X < layout.BoardRect.X);
    }

    [Fact]
    public void Vertical_FretNumberIsLeftOfFirstVisibleFret()
    {
        var layout = ChordLayout.Create(CanvasFormat.Landscape16x9.ToSize(), FretboardOrientation.Vertical, new FretRange(5, 5));
        var (point, anchor) = layout.FretNumberAnchor();

        Assert.Equal(TextAnchor.RightMiddle, anchor);
        Assert.True(point.X < layout.StringOffset(6) - layout.MarkerRadius);
        Assert.Equal(layout.CellCenter(6, 5).Y, point.Y, 6);
    }

    [Fact]
    public void Horizontal_FretNumberIsBelowFirstVisibleFret()
    {
        var layout = ChordLayout.Create(CanvasFormat.Landscape16x9.ToSize(), FretboardOrientation.Horizontal, new FretRange(5, 5));
        var (point, anchor) = layout.FretNumberAnchor();

        Assert.Equal(TextAnchor.TopCenter, anchor);
        Assert.True(point.Y > layout.StringOffset(6) + layout.MarkerRadius);
        Assert.Equal(layout.CellCenter(6, 5).X, point.X, 6);
    }

    [Fact]
    public void StringThickness_DecreasesFrom6To1()
    {
        var layout = ChordLayout.Create(new CanvasSize(1920, 1080), FretboardOrientation.Vertical, new FretRange(1, 5));
        for (var s = 6; s > 1; s--)
        {
            Assert.True(layout.StringThickness(s) > layout.StringThickness(s - 1));
        }
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void EverythingFitsInSafeArea(FretboardOrientation orientation, CanvasFormat canvas)
    {
        foreach (var range in new[] { new FretRange(1, 5), new FretRange(12, 7), new FretRange(20, 5) })
        {
            var layout = ChordLayout.Create(canvas.ToSize(), orientation, range);
            var safe = layout.SafeArea;
            var full = new RectD(0, 0, canvas.ToSize().Width, canvas.ToSize().Height);

            Assert.True(full.Contains(safe));
            Assert.True(safe.Contains(layout.TitleRect));
            Assert.True(safe.Contains(layout.DiagramBounds));
            Assert.True(layout.DiagramBounds.Contains(layout.BoardRect));
            Assert.True(layout.DiagramBounds.Contains(layout.FretNumberBounds(2)));
            Assert.True(layout.DiagramBounds.Contains(layout.BarreRect(range.Start)));
            Assert.True(layout.DiagramBounds.Contains(layout.BarreRect(range.End)));
            Assert.False(Overlaps(layout.TitleRect, layout.DiagramBounds));

            for (var s = 1; s <= 6; s++)
            {
                var ind = layout.IndicatorCenter(s);
                var r = layout.IndicatorRadius;
                Assert.True(layout.DiagramBounds.Contains(new RectD(ind.X - r, ind.Y - r, 2 * r, 2 * r)));
                foreach (var fret in new[] { range.Start, range.End })
                {
                    var c = layout.CellCenter(s, fret);
                    var m = layout.MarkerRadius;
                    Assert.True(layout.DiagramBounds.Contains(new RectD(c.X - m, c.Y - m, 2 * m, 2 * m)));
                }
            }

            Assert.True(layout.Unit > 40, $"Схема слишком мелкая: шаг {layout.Unit:F1}px");
        }
    }

    [Fact]
    public void CellOutsideRange_Throws()
    {
        var layout = ChordLayout.Create(new CanvasSize(1920, 1080), FretboardOrientation.Vertical, new FretRange(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.CellCenter(1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.CellCenter(1, 10));
    }

    private static bool Overlaps(RectD a, RectD b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}
