using MajorMode.Core;
using MajorMode.Core.Parsing;

namespace MajorMode.Tests.Core;

public class DescriptionTests
{
    [Fact]
    public void F_DescriptionMatchesSpecExample()
    {
        var analysis = ChordAnalyzer.Analyze(TestChords.F);

        Assert.Equal(
            "Аккорд F. Баррэ первым пальцем на 1 ладу, все струны. Второй палец: 2 лад, 3 струна. " +
            "Третий палец: 3 лад, 5 струна. Четвертый палец: 3 лад, 4 струна. Заглушенных нет. " +
            "Открытых нет. Отображаются лады 1-5.",
            analysis.Description);
    }

    [Fact]
    public void C_DescriptionListsMutedAndOpen()
    {
        var text = ChordAnalyzer.Analyze(TestChords.C).Description;

        Assert.StartsWith("Аккорд C. Первый палец: 1 лад, 2 струна. Второй палец: 2 лад, 4 струна. Третий палец: 3 лад, 5 струна.", text);
        Assert.Contains("Заглушена 6 струна.", text);
        Assert.Contains("Открытые струны: 3, 1.", text);
        Assert.DoesNotContain("Четвертый", text);
    }

    [Fact]
    public void NoFingers_IsDescribed()
    {
        var text = ChordAnalyzer.Analyze(TestChords.AllUnused).Description;
        Assert.Contains("Пальцы не заданы.", text);
        Assert.Contains("Отображаются лады 1-5.", text);
    }

    [Fact]
    public void Conflict_IsMentioned()
    {
        var text = ChordAnalyzer.Analyze(ChordInput.Create("X", "5,6", new FingerInput("3", "5"))).Description;
        Assert.Contains("Заглушены струны: 6, 5.", text);
        Assert.Contains("на 5 струне есть палец", text);
    }

    [Fact]
    public void InvalidChord_DescriptionExplainsErrors()
    {
        var analysis = ChordAnalyzer.Analyze(ChordInput.Create("X", "", new FingerInput("0", "3")));
        Assert.False(analysis.CanRender);
        Assert.StartsWith("Картинка не построена", analysis.Description);
        Assert.StartsWith("Ошибок: 1.", ChordAnalyzer.BuildErrorSummary(analysis.Messages));
    }
}
