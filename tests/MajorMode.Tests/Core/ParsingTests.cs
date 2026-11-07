using MajorMode.Core.Model;
using MajorMode.Core.Parsing;

namespace MajorMode.Tests.Core;

public class ParsingTests
{
    [Fact]
    public void C_ParsesThreeFingersAndMuted6()
    {
        var chord = TestChords.Parse(TestChords.C);

        Assert.Equal("C", chord.Name);
        Assert.Equal(3, chord.Fingers.Count);
        Assert.Equal(FingerPlacement.OnString(1, 1, 2), chord.Fingers[0]);
        Assert.Equal(FingerPlacement.OnString(2, 2, 4), chord.Fingers[1]);
        Assert.Equal(FingerPlacement.OnString(3, 3, 5), chord.Fingers[2]);
        Assert.Equal([6], chord.MutedStrings);
        Assert.Equal([3, 1], chord.OpenStrings);
    }

    [Fact]
    public void F_ParsesBarreOnFirstFinger()
    {
        var chord = TestChords.Parse(TestChords.F);

        Assert.NotNull(chord.Barre);
        Assert.Equal(1, chord.Barre!.Fret);
        Assert.Equal(1, chord.Barre.Finger);
        Assert.Equal(4, chord.Fingers.Count);
        Assert.Empty(chord.OpenStrings);
        for (var s = 1; s <= 6; s++)
        {
            Assert.Equal(StringState.Fretted, chord.GetStringState(s));
        }
    }

    [Theory]
    [InlineData("b")]
    [InlineData("B")]
    [InlineData(" b ")]
    public void LowercaseB_IsNormalizedToBarre(string text)
    {
        var chord = TestChords.Parse(ChordInput.Create("X", "", new FingerInput("5", text)));
        Assert.True(chord.Fingers.Single().IsBarre);
    }

    [Fact]
    public void AllFingersZero_IsValidChordWithoutFingers()
    {
        var chord = TestChords.Parse(TestChords.AllUnused);
        Assert.Empty(chord.Fingers);
        Assert.Equal(6, chord.OpenStrings.Count);
    }

    [Theory]
    [InlineData("00", "00")]
    [InlineData("0", "00")]
    [InlineData("", "")]
    public void ZeroZero_MeansFingerNotUsed_NotOpenString(string fret, string str)
    {
        var result = ChordParser.Parse(ChordInput.Create("C", "", new FingerInput(fret, str)));
        Assert.True(result.IsValid);
        Assert.Empty(result.Chord!.Fingers);
    }

    [Theory]
    [InlineData("0", "3", FieldId.Finger1Fret)]
    [InlineData("3", "0", FieldId.Finger1String)]
    public void OnlyOneZero_IsError(string fret, string str, FieldId field)
    {
        var result = ChordParser.Parse(ChordInput.Create("C", "", new FingerInput(fret, str)));
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(field, error.Field);
        Assert.Contains("0 в оба поля", error.Text);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Barre_OnFingers2To4_IsError(int finger)
    {
        var fingers = new FingerInput[4];
        for (var i = 0; i < 4; i++)
        {
            fingers[i] = i == finger - 1 ? new FingerInput("3", "B") : FingerInput.Unused;
        }

        var result = ChordParser.Parse(ChordInput.Create("X", "", fingers));
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(FieldIds.FingerString(finger), error.Field);
        Assert.Contains("только для первого пальца", error.Text);
    }

    [Theory]
    [InlineData("25")]
    [InlineData("100")]
    public void FretAboveMax_IsError(string fret)
    {
        var result = ChordParser.Parse(ChordInput.Create("X", "", new FingerInput(fret, "3")));
        var error = Assert.Single(result.Errors);
        Assert.Equal(FieldId.Finger1Fret, error.Field);
        Assert.Contains("от 1 до 24", error.Text);
    }

    [Fact]
    public void MaxFret_IsConfigurable()
    {
        var rules = new ChordRules { MaxFret = 12 };
        Assert.False(ChordParser.Parse(ChordInput.Create("X", "", new FingerInput("13", "3")), rules).IsValid);
        Assert.True(ChordParser.Parse(ChordInput.Create("X", "", new FingerInput("12", "3")), rules).IsValid);
    }

    [Theory]
    [InlineData("-1", "3", FieldId.Finger1Fret)]
    [InlineData("abc", "3", FieldId.Finger1Fret)]
    [InlineData("3", "7", FieldId.Finger1String)]
    [InlineData("3", "x", FieldId.Finger1String)]
    [InlineData("3", "\u0412", FieldId.Finger1String)]
    public void InvalidValues_AreErrorsOnTheRightField(string fret, string str, FieldId field)
    {
        var result = ChordParser.Parse(ChordInput.Create("X", "", new FingerInput(fret, str)));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == field);
    }

    [Fact]
    public void TwoFingersInSamePoint_IsError()
    {
        var result = ChordParser.Parse(ChordInput.Create("X", "", new FingerInput("2", "3"), new FingerInput("2", "3")));
        Assert.False(result.IsValid);
        Assert.Equal(FieldId.Finger2String, Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void FingerOnMutedString_IsWarning_AndDataUnchanged()
    {
        var result = ChordParser.Parse(ChordInput.Create("X", "5", new FingerInput("3", "5")));

        Assert.True(result.IsValid);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(FieldId.MutedStrings, warning.Field);
        Assert.Contains("Струна 5 заглушена", warning.Text);

        var chord = result.Chord!;
        Assert.Equal(FingerPlacement.OnString(1, 3, 5), chord.Fingers.Single());
        Assert.Contains(5, chord.MutedStrings);
        Assert.Equal(StringState.Muted, chord.GetStringState(5));
    }

    [Fact]
    public void BarreWithMutedString_IsWarning()
    {
        var result = ChordParser.Parse(ChordInput.Create("X", "6", new FingerInput("5", "B")));
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Text.Contains("баррэ"));
    }

    [Fact]
    public void DisplayPriority_MutedOverFrettedOverOpen()
    {
        var chord = new Chord("X", [FingerPlacement.OnString(1, 2, 3), FingerPlacement.OnString(2, 2, 4)], [4, 6]);

        Assert.Equal(StringState.Muted, chord.GetStringState(4));
        Assert.Equal(StringState.Muted, chord.GetStringState(6));
        Assert.Equal(StringState.Fretted, chord.GetStringState(3));
        Assert.Equal(StringState.Open, chord.GetStringState(1));
        Assert.Equal([4], chord.MutedButFrettedStrings);
    }

    [Fact]
    public void ChordName_IsNotValidatedHarmonically()
    {
        var chord = TestChords.Parse(ChordInput.Create("  F#m7(b5) что угодно ", ""));
        Assert.Equal("F#m7(b5) что угодно", chord.Name);
    }
}
