using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record AdminBridgeResult(bool Success, string Status, string RawJson, string Summary);

public static class AdminBridgeService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<AdminBridgeResult> CallAsync(
        AppSettings settings,
        string? token,
        HttpMethod method,
        string path,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(settings.AdminBridgeUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return new AdminBridgeResult(false, "Admin Bridge URL non valido.", string.Empty, "Usa http/https valido.");
        }

        try
        {
            using var request = new HttpRequestMessage(method, new Uri(baseUri, path));
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("ChatClaw-Windows");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim());
            }

            if (payload is not null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            }

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var raw = PrettyJson(body);
            return new AdminBridgeResult(
                response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? "Admin Bridge OK." : $"Admin Bridge HTTP {(int)response.StatusCode}.",
                raw,
                Summarize(raw));
        }
        catch (Exception ex)
        {
            return new AdminBridgeResult(false, "Admin Bridge fallito.", string.Empty, ex.Message);
        }
    }

    private static string PrettyJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }
        catch
        {
            return raw;
        }
    }

    private static string Summarize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Nessun contenuto.";
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            foreach (var key in new[] { "status", "message", "summary", "error" })
            {
                if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? raw;
                }
            }
        }
        catch
        {
            // Raw text is already useful.
        }

        return raw.Length <= 220 ? raw : raw[..220] + "...";
    }
}
