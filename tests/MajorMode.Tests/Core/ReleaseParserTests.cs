using MajorMode.Core.Updates;

namespace MajorMode.Tests.Core;

public class ReleaseParserTests
{
    private const string Sample = """
        {
          "tag_name": "v0.1.42",
          "html_url": "https://github.com/owner/repo/releases/tag/v0.1.42",
          "assets": [
            { "name": "MajorMode.exe.sha256", "size": 80, "browser_download_url": "https://github.com/owner/repo/releases/download/v0.1.42/MajorMode.exe.sha256" },
            { "name": "MajorMode.exe", "size": 68000000, "browser_download_url": "https://github.com/owner/repo/releases/download/v0.1.42/MajorMode.exe" }
          ]
        }
        """;

    [Fact]
    public void Parse_ReadsVersionAndAssets()
    {
        var release = ReleaseParser.Parse(Sample);

        Assert.NotNull(release);
        Assert.Equal(new Version(0, 1, 42), release.Version);
        Assert.Equal("v0.1.42", release.Tag);
        Assert.EndsWith("/MajorMode.exe", release.ExeUrl);
        Assert.Equal(68000000, release.ExeSize);
        Assert.EndsWith("/MajorMode.exe.sha256", release.Sha256Url);
        Assert.Contains("releases/tag", release.PageUrl);
    }

    [Theory]
    [InlineData("""{ "tag_name": "v0.1.42", "assets": [] }""")]
    [InlineData("""{ "tag_name": "latest", "assets": [ { "name": "MajorMode.exe", "browser_download_url": "https://x/MajorMode.exe" } ] }""")]
    [InlineData("""{ "tag_name": "v0.1.42", "assets": [ { "name": "MajorMode.exe", "browser_download_url": "http://x/MajorMode.exe" } ] }""")]
    [InlineData("""{ "message": "Not Found" }""")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void Parse_ReturnsNull_WhenReleaseUnusable(string json) => Assert.Null(ReleaseParser.Parse(json));

    [Theory]
    [InlineData("v0.1.42", "0.1.42")]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("V1.0.7.0", "1.0.7")]
    public void ParseTag_Normalizes(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), ReleaseParser.ParseTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1")]
    [InlineData("release")]
    public void ParseTag_RejectsInvalid(string? tag) => Assert.Null(ReleaseParser.ParseTag(tag));

    [Theory]
    [InlineData("0.1.0", "0.1.42", true)]
    [InlineData("0.1.42.0", "0.1.42", false)]
    [InlineData("0.1.43", "0.1.42", false)]
    [InlineData("0.1.99", "0.2.1", true)]
    public void IsNewer_ComparesNormalized(string current, string candidate, bool expected) =>
        Assert.Equal(expected, ReleaseParser.IsNewer(Version.Parse(current), Version.Parse(candidate)));

    [Fact]
    public void ParseChecksum_ReadsSha256SumFormat()
    {
        var hash = new string('A', 64);
        Assert.Equal(new string('a', 64), ReleaseParser.ParseChecksum($"{hash}  MajorMode.exe\r\n"));
        Assert.Null(ReleaseParser.ParseChecksum("xyz  MajorMode.exe"));
        Assert.Null(ReleaseParser.ParseChecksum(""));
    }
}
