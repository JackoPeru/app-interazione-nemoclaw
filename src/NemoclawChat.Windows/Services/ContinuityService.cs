using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace NemoclawChat_Windows.Services;

public sealed record ContinuityItem(string Id, string Type, string Device, string Value, string ConversationId, string ProjectId, string FileUrl, string FileName, DateTimeOffset UpdatedAt, string Status);

public static class ContinuityService
{
    private const string QueueKey = "ContinuityOfflineQueue";
    public static string DeviceId => $"windows-{Environment.MachineName}".ToLowerInvariant();

    public static async Task<(IReadOnlyList<ContinuityItem> Items, string Status)> LoadAsync(AppSettings settings)
    {
        var raw = await GatewayService.SendHermesRequestAsync(settings, HttpMethod.Get, "/v1/hub/state");
        try
        {
            using var document = JsonDocument.Parse(raw);
            var items = new List<ContinuityItem>();
            foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                var type = Text(item, "type");
                if (!type.StartsWith("continuity.", StringComparison.OrdinalIgnoreCase)) continue;
                var updated = Number(item, "updated_at");
                items.Add(new ContinuityItem(Text(item, "id"), type, Text(item, "device"), Text(item, "value"), Text(item, "conversation_id"), Text(item, "project_id"), Text(item, "file_url"), Text(item, "file_name"), updated > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)updated) : DateTimeOffset.Now, Text(item, "status")));
            }
            return (items.OrderByDescending(item => item.UpdatedAt).ToList(), "Sincronizzato ora.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return ([], $"Offline: {ex.Message}"); }
    }

    public static async Task<string> PublishAsync(AppSettings settings, string type, string value = "", string conversationId = "", string projectId = "", string fileUrl = "", string fileName = "", string status = "available")
    {
        var id = $"{type}:{DeviceId}";
        var payload = JsonSerializer.Serialize(new { id, type, device = DeviceId, value, conversation_id = conversationId, project_id = projectId, file_url = fileUrl, file_name = fileName, status, updated_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        var result = await GatewayService.SendHermesRequestAsync(settings, HttpMethod.Post, "/v1/hub/state", payload);
        if (!result.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase)) return "Stato pubblicato.";
        Enqueue(payload);
        return "Gateway offline: operazione accodata localmente.";
    }

    public static async Task<string> FlushQueueAsync(AppSettings settings)
    {
        var queue = LoadQueue();
        var remaining = new List<string>();
        foreach (var payload in queue)
        {
            var result = await GatewayService.SendHermesRequestAsync(settings, HttpMethod.Post, "/v1/hub/state", payload);
            if (result.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase)) remaining.Add(payload);
        }
        SaveQueue(remaining);
        return remaining.Count == 0 ? "Coda offline sincronizzata." : $"{remaining.Count} operazioni ancora in coda.";
    }

    public static async Task<string> ClipboardTextAsync()
    {
        var view = Clipboard.GetContent();
        return view.Contains(StandardDataFormats.Text) ? await view.GetTextAsync() : string.Empty;
    }

    public static void SetClipboard(string text) { var package = new DataPackage(); package.SetText(text); Clipboard.SetContent(package); }
    public static int QueueCount => LoadQueue().Count;
    private static void Enqueue(string payload) { var queue = LoadQueue(); queue.Add(payload); SaveQueue(queue.TakeLast(100).ToList()); }
    private static List<string> LoadQueue() { var raw = ApplicationData.Current.LocalSettings.Values[QueueKey] as string; try { return string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<List<string>>(raw) ?? []; } catch (JsonException) { return []; } }
    private static void SaveQueue(List<string> queue) => ApplicationData.Current.LocalSettings.Values[QueueKey] = JsonSerializer.Serialize(queue);
    private static string Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static double Number(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : 0;
}
