using MajorMode.Core.Files;
using MajorMode.Core.Model;
using MajorMode.Core.Settings;

namespace MajorMode.Tests.Core;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("C", "C")]
    [InlineData("C#", "C#")]
    [InlineData("Bb", "Bb")]
    [InlineData("F#m7", "F#m7")]
    [InlineData("A/E", "A_E")]
    [InlineData("C:maj?", "C_maj_")]
    [InlineData("a\\b*c\"d<e>f|g", "a_b_c_d_e_f_g")]
    [InlineData("Dm. . ", "Dm")]
    [InlineData("  Am  ", "Am")]
    [InlineData("", "chord")]
    [InlineData("   ", "chord")]
    [InlineData("...", "chord")]
    [InlineData(null, "chord")]
    [InlineData("C.gif", "C")]
    [InlineData("C.JPEG", "C")]
    [InlineData("tab\tname", "tab_name")]
    public void Sanitize(string? input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
    }

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("con", "con_")]
    [InlineData("PRN", "PRN_")]
    [InlineData("AUX", "AUX_")]
    [InlineData("NUL", "NUL_")]
    [InlineData("COM1", "COM1_")]
    [InlineData("LPT9", "LPT9_")]
    [InlineData("CON.backup", "CON_.backup")]
    [InlineData("CONSOLE", "CONSOLE")]
    [InlineData("COM10", "COM10")]
    public void ReservedNames_GetSuffix(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void LongName_IsTruncated()
    {
        var name = FileNameSanitizer.Sanitize(new string('a', 300));
        Assert.Equal(FileNameSanitizer.MaxBaseNameLength, name.Length);
    }

    [Theory]
    [InlineData(ExportFormat.Gif, "C#.gif")]
    [InlineData(ExportFormat.Png, "C#.png")]
    [InlineData(ExportFormat.Jpg, "C#.jpg")]
    public void BuildFileName_AddsExtensionOfFormat(ExportFormat format, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.BuildFileName("C#", format));
    }

    [Fact]
    public void BuildFullPath_RequiresFolder_AndLimitsLength()
    {
        Assert.NotNull(FileNameSanitizer.BuildFullPath("", "C", ExportFormat.Gif).Error);

        var longFolder = "/" + string.Join("/", Enumerable.Repeat(new string('d', 50), 5));
        var (path, error) = FileNameSanitizer.BuildFullPath(longFolder, "C", ExportFormat.Gif);
        Assert.Null(path);
        Assert.Contains("слишком длинный", error);

        var ok = FileNameSanitizer.BuildFullPath("/tmp", "Bb", ExportFormat.Png);
        Assert.Null(ok.Error);
        Assert.EndsWith("Bb.png", ok.Path);
    }

    [Fact]
    public void Settings_RoundTrip_AndBadJsonFallsBack()
    {
        var settings = new AppSettings { OutputFolder = "X", Style = ImageStyle.RealisticWood, Format = ExportFormat.Png };
        var back = AppSettingsSerializer.Deserialize(AppSettingsSerializer.Serialize(settings));
        Assert.Equal(settings, back);

        Assert.Equal(new AppSettings(), AppSettingsSerializer.Deserialize("{not json"));
        Assert.Equal(new AppSettings(), AppSettingsSerializer.Deserialize("{\"SchemaVersion\": 99}"));
        Assert.Contains("\"SchemaVersion\": 1", AppSettingsSerializer.Serialize(new AppSettings()));
    }
}
