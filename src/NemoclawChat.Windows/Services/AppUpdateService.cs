using System.Net.Http;
using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string LocalVersion,
    string? LatestVersion,
    string Message,
    string ReleaseUrl,
    string? AssetUrl);

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
        request.Headers.UserAgent.ParseAdd("NemoclawChat-Windows");
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
                    null);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var latest = NormalizeVersion(tag);
            var releaseUrl = root.GetProperty("html_url").GetString() ?? ReleasesPage;
            var assetUrl = FindAsset(root, [".msix", ".exe", ".zip"]);

            var hasUpdate = CompareVersions(latest, localVersion) > 0;
            var message = hasUpdate
                ? $"Aggiornamento disponibile: {localVersion} -> {latest}."
                : $"App aggiornata. Versione locale: {localVersion}, GitHub: {latest}.";

            if (hasUpdate && assetUrl is null)
            {
                message += " Release trovata, ma manca asset Windows (.msix/.exe/.zip).";
            }

            return new UpdateCheckResult(hasUpdate, localVersion, latest, message, releaseUrl, assetUrl);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                false,
                localVersion,
                null,
                $"Controllo update non riuscito: {ex.Message}",
                ReleasesPage,
                null);
        }
    }

    private static string? FindAsset(JsonElement root, string[] suffixes)
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
                return asset.GetProperty("browser_download_url").GetString();
            }
        }

        return null;
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
}
