using MajorMode.Core;
using MajorMode.Core.Model;
using MajorMode.Core.Parsing;

namespace MajorMode.Tests;

/// <summary>Обязательные тестовые аккорды из ТЗ.</summary>
public static class TestChords
{
    /// <summary>C: 1/2, 2/4, 3/5, палец 4 не используется; для демо заглушена 6 струна.</summary>
    public static ChordInput C => ChordInput.Create("C", "6",
        new("1", "2"), new("2", "4"), new("3", "5"), new("0", "0"));

    public static ChordInput CNoMute => ChordInput.Create("C", "",
        new("1", "2"), new("2", "4"), new("3", "5"), new("0", "0"));

    /// <summary>F: баррэ на 1 ладу, 2/3, 3/5, 3/4.</summary>
    public static ChordInput F => ChordInput.Create("F", "",
        new("1", "B"), new("2", "3"), new("3", "5"), new("3", "4"));

    /// <summary>Баррэ на 5 ладу, остальные пальцы со смещением (только данные, без теории).</summary>
    public static ChordInput Barre5 => ChordInput.Create("Barre 5", "",
        new("5", "B"), new("7", "5"), new("7", "4"), new("6", "3"));

    /// <summary>A: малое баррэ третьим пальцем на 2 ладу, струны 4-2; 6 заглушена.</summary>
    public static ChordInput APartialBarre => ChordInput.Create("A", "6",
        new("0", "0"), new("0", "0"), new("2", "4,3,2"), new("0", "0"));

    public static ChordInput AllUnused => ChordInput.Create("Пусто", "");

    public static ChordAnalysis Analyze(ChordInput input) => ChordAnalyzer.Analyze(input);

    public static Chord Parse(ChordInput input)
    {
        var result = ChordParser.Parse(input);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Text)));
        return result.Chord!;
    }
}
