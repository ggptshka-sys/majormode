using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using MajorMode.Core.Updates;

namespace MajorMode.App.Services;

public enum UpdateCheckStatus
{
    UpToDate,
    Available,
    Failed,
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, ReleaseInfo? Release, string Message);

/// <summary>
/// Обновление из GitHub Releases по кнопке, только с подтверждения пользователя.
/// Без сервера, авторизации и телеметрии: один публичный запрос к api.github.com и загрузка exe.
/// </summary>
public sealed class UpdateService
{
    public const string Owner = "ggptshka-sys";
    public const string Repository = "majormode";
    public const string UpdatedFromArgument = "--updated-from";

    private const string OldSuffix = ".old";
    private const string DownloadSuffix = ".download";

    public static Version CurrentVersion { get; } =
        ReleaseParser.Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    private static readonly HttpClient Http = CreateClient();

    public static string CurrentVersionText => CurrentVersion.ToString(3);

    /// <summary>Заменять можно только single-file exe; при запуске из папки сборки рядом лежат dll.</summary>
    public static bool CanSelfUpdate =>
        string.IsNullOrEmpty(typeof(UpdateService).Assembly.Location) && ExePath is not null;

    private static string? ExePath => Environment.ProcessPath;

    public static string ProjectPage => $"https://github.com/{Owner}/{Repository}";

    public static string ReleasesPage => $"https://github.com/{Owner}/{Repository}/releases";

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            // CDN GitHub кэширует ответ ~60 с по URL; уникальный параметр дает свежий релиз сразу после публикации.
            var url = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest?nocache={DateTime.UtcNow.Ticks}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Http.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new(UpdateCheckStatus.Failed, null, "Опубликованных версий пока нет.");
            }

            if ((int)response.StatusCode is 403 or 429)
            {
                return new(UpdateCheckStatus.Failed, null, "GitHub временно ограничил число проверок. Повторите через час.");
            }

            response.EnsureSuccessStatusCode();
            var release = ReleaseParser.Parse(await response.Content.ReadAsStringAsync(ct));
            if (release is null)
            {
                return new(UpdateCheckStatus.Failed, null, "Не удалось прочитать данные о последней версии.");
            }

            return ReleaseParser.IsNewer(CurrentVersion, release.Version)
                ? new(UpdateCheckStatus.Available, release,
                    $"Доступна версия {release.Version.ToString(3)}. Установлена {CurrentVersionText}.")
                : new(UpdateCheckStatus.UpToDate, release, $"Установлена последняя версия {CurrentVersionText}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(UpdateCheckStatus.Failed, null, "Не удалось проверить обновления: нет связи с GitHub. Проверьте интернет.");
        }
    }

    /// <summary>
    /// Скачивает exe рядом с текущим, проверяет SHA-256, переименовывает текущий в .old (Windows разрешает
    /// переименовать запущенный exe), кладет новый на его место и запускает. Возвращает текст ошибки или null.
    /// </summary>
    public async Task<string?> InstallAsync(ReleaseInfo release, IProgress<string> progress, CancellationToken ct = default)
    {
        var exe = ExePath;
        if (!CanSelfUpdate || exe is null)
        {
            return $"Автообновление работает только для MajorMode.exe из релиза. Скачайте новую версию вручную: {ReleasesPage}";
        }

        var download = exe + DownloadSuffix;
        var old = exe + OldSuffix;
        try
        {
            await DownloadAsync(release, download, progress, ct);

            if (release.Sha256Url is not null)
            {
                progress.Report("Проверка файла…");
                var expected = ReleaseParser.ParseChecksum(await Http.GetStringAsync(release.Sha256Url, ct));
                var actual = await ComputeSha256Async(download, ct);
                if (expected is null || expected != actual)
                {
                    File.Delete(download);
                    return "Скачанный файл поврежден (контрольная сумма не совпала). Обновление отменено, программа не изменена.";
                }
            }

            if (File.Exists(old))
            {
                File.Delete(old);
            }

            File.Move(exe, old);
            try
            {
                File.Move(download, exe);
            }
            catch
            {
                File.Move(old, exe);
                throw;
            }

            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                ArgumentList = { UpdatedFromArgument, CurrentVersionText },
            });
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            TryDelete(download);
            return $"Нет прав на запись в папку программы ({Path.GetDirectoryName(exe)}). Переместите MajorMode.exe, например, в папку «Документы» и повторите.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TryDelete(download);
            return "Загрузка прервалась: нет связи с GitHub. Программа не изменена, повторите позже.";
        }
        catch (IOException ex)
        {
            TryDelete(download);
            return $"Не удалось записать обновление: {ex.Message} Программа не изменена.";
        }
    }

    /// <summary>Удаляет exe прошлой версии после перезапуска. Старый процесс может еще завершаться — несколько попыток.</summary>
    public static async Task CleanupAsync()
    {
        var exe = ExePath;
        if (exe is null)
        {
            return;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (TryDelete(exe + OldSuffix) && TryDelete(exe + DownloadSuffix))
            {
                return;
            }

            await Task.Delay(500);
        }
    }

    private static async Task DownloadAsync(ReleaseInfo release, string target, IProgress<string> progress, CancellationToken ct)
    {
        using var response = await Http.GetAsync(release.ExeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? release.ExeSize;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
        var buffer = new byte[1 << 16];
        long done = 0;
        var lastReported = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            var mb = (int)(done / (1024 * 1024));
            if (mb != lastReported)
            {
                lastReported = mb;
                progress.Report(total > 0
                    ? $"Загрузка обновления: {mb} из {total / (1024 * 1024)} МБ"
                    : $"Загрузка обновления: {mb} МБ");
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MajorMode/{CurrentVersionText}");
        return client;
    }
}
