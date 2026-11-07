using System.Text.Json;

namespace MajorMode.Core.Updates;

/// <summary>Релиз GitHub с готовым exe. Версия берется из тега вида v0.1.42.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string ExeUrl, long ExeSize, string? Sha256Url, string PageUrl);

public static class ReleaseParser
{
    public const string ExeAssetName = "MajorMode.exe";
    public const string ChecksumAssetName = "MajorMode.exe.sha256";

    /// <summary>Разбирает ответ GET /repos/{owner}/{repo}/releases/latest. Null, если тег или exe не найдены.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var tag = GetString(root, "tag_name");
            var version = ParseTag(tag);
            if (version is null || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? exeUrl = null;
            string? shaUrl = null;
            long exeSize = 0;
            foreach (var asset in assets.EnumerateArray())
            {
                var name = GetString(asset, "name");
                var url = GetString(asset, "browser_download_url");
                if (url is null || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(name, ExeAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    exeUrl = url;
                    exeSize = asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var s) ? s : 0;
                }
                else if (string.Equals(name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    shaUrl = url;
                }
            }

            return exeUrl is null
                ? null
                : new ReleaseInfo(version, tag!, exeUrl, exeSize, shaUrl, GetString(root, "html_url") ?? string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var v) && v.Build >= 0 ? Normalize(v) : null;
    }

    /// <summary>0.1.42 и 0.1.42.0 — одна версия.</summary>
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    public static bool IsNewer(Version current, Version candidate) => Normalize(candidate) > Normalize(current);

    /// <summary>Формат sha256sum: "hash  имя". Возвращает hex в нижнем регистре или null.</summary>
    public static string? ParseChecksum(string text)
    {
        var token = text.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is { Length: 64 } && token.All(Uri.IsHexDigit) ? token.ToLowerInvariant() : null;
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
