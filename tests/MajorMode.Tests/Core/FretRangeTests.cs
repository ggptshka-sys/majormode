using MajorMode.Core;
using MajorMode.Core.Layout;
using MajorMode.Core.Model;
using MajorMode.Core.Parsing;

namespace MajorMode.Tests.Core;

public class FretRangeTests
{
    private static FretRange RangeOf(ChordInput input)
    {
        var result = FretRangeCalculator.Calculate(TestChords.Parse(input));
        Assert.True(result.IsValid, result.Error);
        return result.Range!.Value;
    }

    [Fact]
    public void C_ShowsFrets1To5WithNut()
    {
        var range = RangeOf(TestChords.C);
        Assert.Equal(new FretRange(1, 5), range);
        Assert.True(range.ShowNut);
    }

    [Fact]
    public void F_Barre_StartsAtFret1()
    {
        Assert.Equal(new FretRange(1, 5), RangeOf(TestChords.F));
    }

    [Fact]
    public void Barre5_StartsAt5()
    {
        var range = RangeOf(TestChords.Barre5);
        Assert.Equal(5, range.Start);
        Assert.Equal(5, range.Count);
        Assert.False(range.ShowNut);
    }

    [Fact]
    public void Barre5_FretNumberLabelIs5_InDescription()
    {
        var analysis = ChordAnalyzer.Analyze(TestChords.Barre5);
        Assert.Contains("Отображаются лады 5-9.", analysis.Description);
    }

    [Fact]
    public void Barre_WithFingerBelow_StartsAtMinFret()
    {
        var range = RangeOf(ChordInput.Create("X", "", new FingerInput("7", "B"), new FingerInput("6", "3")));
        Assert.Equal(6, range.Start);
    }

    [Fact]
    public void NoFingers_Shows1To5()
    {
        Assert.Equal(new FretRange(1, 5), RangeOf(TestChords.AllUnused));
    }

    [Fact]
    public void HighPosition_StartsAtMinFret()
    {
        Assert.Equal(new FretRange(7, 5), RangeOf(ChordInput.Create("X", "", new FingerInput("7", "2"), new FingerInput("9", "4"))));
    }

    [Fact]
    public void SpanOf6_IsExtended()
    {
        Assert.Equal(new FretRange(3, 6), RangeOf(ChordInput.Create("X", "", new FingerInput("3", "2"), new FingerInput("8", "4"))));
    }

    [Fact]
    public void UnreadableSpan_IsErrorNotTruncated()
    {
        var chord = TestChords.Parse(ChordInput.Create("X", "", new FingerInput("1", "2"), new FingerInput("12", "4")));
        var result = FretRangeCalculator.Calculate(chord);
        Assert.False(result.IsValid);
        Assert.Contains("не больше 7", result.Error);

        var analysis = ChordAnalyzer.Analyze(ChordInput.Create("X", "", new FingerInput("1", "2"), new FingerInput("12", "4")));
        Assert.False(analysis.CanRender);
        Assert.Contains(analysis.Errors, e => e.Field == FieldId.General);
    }

    [Fact]
    public void NearLastFret_RangeDoesNotExceedMaxFret()
    {
        var range = RangeOf(ChordInput.Create("X", "", new FingerInput("23", "2"), new FingerInput("24", "4")));
        Assert.Equal(24, range.End);
        Assert.Equal(20, range.Start);
    }

    [Fact]
    public void MaxVisibleFrets_IsConfigurable()
    {
        var chord = TestChords.Parse(ChordInput.Create("X", "", new FingerInput("3", "2"), new FingerInput("10", "4")));
        Assert.False(FretRangeCalculator.Calculate(chord).IsValid);
        Assert.True(FretRangeCalculator.Calculate(chord, new ChordRules { MaxVisibleFrets = 8 }).IsValid);
    }
}
