using System.Text.Json;
using System.Text.Json.Serialization;
using MajorMode.Core.Model;

namespace MajorMode.Core.Settings;

/// <summary>
/// Пользовательские настройки между запусками. Аккорды сюда не сохраняются.
/// При изменении формата увеличивайте <see cref="CurrentSchemaVersion"/> и сохраняйте чтение старых версий.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string? OutputFolder { get; init; }

    public FretboardOrientation Orientation { get; init; } = FretboardOrientation.Vertical;

    public CanvasFormat Canvas { get; init; } = CanvasFormat.Landscape16x9;

    public ImageStyle Style { get; init; } = ImageStyle.Schematic;

    public ExportFormat Format { get; init; } = ExportFormat.Gif;
}

public static class AppSettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    /// <summary>Битые или незнакомые данные дают настройки по умолчанию, без исключений.</summary>
    public static AppSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            {
                return new AppSettings();
            }

            return settings with { SchemaVersion = AppSettings.CurrentSchemaVersion };
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }
}
