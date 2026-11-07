using MajorMode.Core.Parsing;

namespace MajorMode.Tests.Core;

public class MutedStringsParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("XX")]
    [InlineData("xx")]
    [InlineData("\u0425\u0425")]
    [InlineData("\u0445\u0445")]
    [InlineData("X")]
    public void NoneTokens_MeanNoMutedStrings(string? text)
    {
        var result = MutedStringsParser.Parse(text);
        Assert.True(result.IsValid);
        Assert.Empty(result.Strings!);
    }

    [Theory]
    [InlineData("6", new[] { 6 })]
    [InlineData("6,5", new[] { 5, 6 })]
    [InlineData("6, 5", new[] { 5, 6 })]
    [InlineData("6;5", new[] { 5, 6 })]
    [InlineData(" 6 5 ", new[] { 5, 6 })]
    [InlineData("1,1,2", new[] { 1, 2 })]
    public void Lists_AreParsed(string text, int[] expected)
    {
        var result = MutedStringsParser.Parse(text);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Strings!.OrderBy(s => s));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("65")]
    [InlineData("a")]
    [InlineData("6,x")]
    [InlineData("XX6")]
    public void InvalidTokens_AreErrors(string text)
    {
        var result = MutedStringsParser.Parse(text);
        Assert.False(result.IsValid);
        Assert.Contains("6,5", result.Error);
    }
}
