using System.Net.Http;
using System.Text.Json;
using Windows.Storage;
using Windows.System;

namespace NemoclawChat_Windows.Services;

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string LocalVersion,
    string? LatestVersion,
    string Message,
    string ReleaseUrl,
    string? AssetUrl,
    string? AssetName);

public sealed record UpdateDownloadProgress(double? Percent, string Status, string Detail);

public static class AppUpdateService
{
    public const string RepositoryOwner = "JackoPeru";
    public const string RepositoryName = "app-interazione-nemoclaw";
    public const string ReleasesPage = "https://github.com/JackoPeru/app-interazione-nemoclaw/releases";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task<UpdateCheckResult> CheckAsync(string localVersion)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");
        request.Headers.UserAgent.ParseAdd("HermesHub-Windows");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        try
        {
            using var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    false,
                    localVersion,
                    null,
                    "Nessuna release GitHub trovata. Crea una release con tag vX.Y.Z e asset Windows/Android.",
                    ReleasesPage,
                    null,
                    null);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var latest = NormalizeVersion(tag);
            var releaseUrl = root.GetProperty("html_url").GetString() ?? ReleasesPage;
            var asset = FindAsset(root, [".msix", ".exe", ".zip"]);

            var hasUpdate = CompareVersions(latest, localVersion) > 0;
            var message = hasUpdate
                ? $"Aggiornamento disponibile: {localVersion} -> {latest}."
                : $"App aggiornata. Versione locale: {localVersion}, GitHub: {latest}.";

            if (hasUpdate && asset is null)
            {
                message += " Release trovata, ma manca asset Windows (.msix/.exe/.zip).";
            }

            return new UpdateCheckResult(hasUpdate, localVersion, latest, message, releaseUrl, asset?.Url, asset?.Name);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                false,
                localVersion,
                null,
                $"Controllo update non riuscito: {ex.Message}",
                ReleasesPage,
                null,
                null);
        }
    }

    public static FileInfo? FindDownloadedAsset(string version, string? assetName)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var updatesDirectory = GetUpdatesDirectoryPath();
        if (!Directory.Exists(updatesDirectory))
        {
            return null;
        }

        var normalizedVersion = NormalizeVersion(version);

        if (!string.IsNullOrWhiteSpace(assetName))
        {
            var exactPath = Path.Combine(updatesDirectory, assetName);
            if (File.Exists(exactPath))
            {
                return new FileInfo(exactPath);
            }
        }

        return new DirectoryInfo(updatesDirectory)
            .GetFiles()
            .FirstOrDefault(file => file.Name.Contains(normalizedVersion, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<string?> DownloadAssetAsync(
        string assetUrl,
        string version,
        string? assetName,
        IProgress<UpdateDownloadProgress>? progress)
    {
        var updatesDirectory = GetUpdatesDirectoryPath();
        Directory.CreateDirectory(updatesDirectory);

        var fileName = !string.IsNullOrWhiteSpace(assetName)
            ? assetName
            : $"ChatClaw-{NormalizeVersion(version)}{GuessExtension(assetUrl)}";
        var destinationPath = Path.Combine(updatesDirectory, fileName);

        using var request = new HttpRequestMessage(HttpMethod.Get, assetUrl);
        request.Headers.UserAgent.ParseAdd("HermesHub-Windows");
        request.Headers.Accept.ParseAdd("application/octet-stream");

        try
        {
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var total = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(destinationPath);
            var buffer = new byte[81920];
            long readTotal = 0;

            while (true)
            {
                var read = await input.ReadAsync(buffer);
                if (read <= 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;

                if (total is > 0)
                {
                    var percent = (double)readTotal / total.Value * 100d;
                    progress?.Report(new UpdateDownloadProgress(
                        percent,
                        "Scaricamento update...",
                        $"{ToReadableSize(readTotal)} / {ToReadableSize(total.Value)}"));
                }
                else
                {
                    progress?.Report(new UpdateDownloadProgress(null, "Scaricamento update...", ToReadableSize(readTotal)));
                }
            }

            progress?.Report(new UpdateDownloadProgress(100d, "Download completato.", ToReadableSize(readTotal)));
            return destinationPath;
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            return null;
        }
    }

    public static async Task<bool> LaunchDownloadedAssetAsync(string path)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            return await Launcher.LaunchFileAsync(file);
        }
        catch
        {
            return false;
        }
    }

    private static string GetUpdatesDirectoryPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "ChatClaw", "updates");
    }

    private static AssetReference? FindAsset(JsonElement root, string[] suffixes)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            if (suffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                return new AssetReference(name, asset.GetProperty("browser_download_url").GetString());
            }
        }

        return null;
    }

    private static string GuessExtension(string assetUrl)
    {
        var extension = Path.GetExtension(assetUrl);
        return string.IsNullOrWhiteSpace(extension) ? ".bin" : extension;
    }

    private static string ToReadableSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var units = new[] { "B", "KB", "MB", "GB" };
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{value:F1} {units[unitIndex]}";
    }

    private static string NormalizeVersion(string version)
    {
        return version.Trim().TrimStart('v', 'V');
    }

    private static int CompareVersions(string latest, string local)
    {
        return ParseVersion(latest).CompareTo(ParseVersion(local));
    }

    private static Version ParseVersion(string version)
    {
        var clean = NormalizeVersion(version).Split('-', '+')[0];
        return Version.TryParse(clean, out var parsed) ? parsed : new Version(0, 0, 0);
    }

    private sealed record AssetReference(string Name, string? Url);
}
