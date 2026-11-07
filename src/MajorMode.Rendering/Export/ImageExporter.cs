using MajorMode.Core.Model;
using SkiaSharp;

namespace MajorMode.Rendering.Export;

public sealed record ExportResult(bool Success, string? Path, string Message);

/// <summary>Кодирование и запись картинки. Экспорт содержит только изображение.</summary>
public static class ImageExporter
{
    public const int JpegQuality = 92;

    private const int ErrorSharingViolation = unchecked((int)0x80070020);
    private const int ErrorLockViolation = unchecked((int)0x80070021);
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
    private const int ErrorDiskFull = unchecked((int)0x80070070);

    public static byte[] Encode(ChordImage image, ExportFormat format, ImageStyle style)
    {
        ArgumentNullException.ThrowIfNull(image);
        switch (format)
        {
            case ExportFormat.Gif:
            {
                using var ms = new MemoryStream();
                var reserved = ChordRenderer.KeyColors(style).Select(c => (c.Red << 16) | (c.Green << 8) | c.Blue);
                GifEncoder.Encode(image.GetBgraPixels(), image.Width, image.Height, ms, reserved);
                return ms.ToArray();
            }

            case ExportFormat.Png:
                return EncodeSkia(image, SKEncodedImageFormat.Png, 100);

            case ExportFormat.Jpg:
                return EncodeSkia(image, SKEncodedImageFormat.Jpeg, JpegQuality);

            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    /// <summary>
    /// Пишет файл через временный файл в той же папке, чтобы при сбое не оставить испорченную картинку.
    /// Подтверждение перезаписи запрашивает UI до вызова; здесь <paramref name="overwrite"/> только разрешает замену.
    /// </summary>
    public static ExportResult Save(ChordImage image, ExportFormat format, ImageStyle style, string fullPath, bool overwrite)
    {
        var folder = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return Fail($"Папка не найдена: {folder}. Выберите другую папку.");
        }

        if (File.Exists(fullPath) && !overwrite)
        {
            return Fail($"Файл уже существует: {fullPath}. Перезапись не подтверждена, файл не изменен.");
        }

        string? temp = null;
        try
        {
            var bytes = Encode(image, format, style);
            temp = Path.Combine(folder, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, fullPath, overwrite: true);
            temp = null;
            return new ExportResult(true, fullPath, $"Сохранено: {fullPath}");
        }
        catch (UnauthorizedAccessException)
        {
            return Fail($"Нет прав на запись в папку {folder}, или файл только для чтения. Выберите другую папку.");
        }
        catch (DirectoryNotFoundException)
        {
            return Fail($"Папка не найдена: {folder}. Выберите другую папку.");
        }
        catch (PathTooLongException)
        {
            return Fail("Путь к файлу слишком длинный. Выберите папку ближе к корню диска или сократите имя файла.");
        }
        catch (IOException ex) when (ex.HResult is ErrorDiskFull or ErrorHandleDiskFull)
        {
            return Fail("Недостаточно места на диске. Освободите место или выберите другой диск.");
        }
        catch (IOException ex) when (ex.HResult is ErrorSharingViolation or ErrorLockViolation)
        {
            return Fail($"Файл {fullPath} занят другой программой (например, REAPER). Закройте его там или сохраните под другим именем.");
        }
        catch (IOException ex)
        {
            return Fail($"Не удалось сохранить файл: {ex.Message}");
        }
        finally
        {
            if (temp is not null)
            {
                TryDelete(temp);
            }
        }

        static ExportResult Fail(string message) => new(false, null, $"Ошибка сохранения. {message}");
    }

    private static byte[] EncodeSkia(ChordImage image, SKEncodedImageFormat format, int quality)
    {
        using var data = image.Bitmap.Encode(format, quality)
            ?? throw new InvalidOperationException($"SkiaSharp не смог закодировать {format}.");
        return data.ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
