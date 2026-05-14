using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record AgentTaskRecord(
    string Id,
    string? RemoteId,
    string Title,
    string Mode,
    string Status,
    string Detail,
    bool RequiresApproval,
    string Source,
    DateTimeOffset UpdatedAt);

public static class AgentTaskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string CurrentDirectoryName = "ChatClaw";

    private static string StorePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var currentDirectory = Path.Combine(localAppData, CurrentDirectoryName);
            Directory.CreateDirectory(currentDirectory);
            return Path.Combine(currentDirectory, "tasks.json");
        }
    }

    public static List<AgentTaskRecord> Load()
    {
        var content = AtomicJsonFile.Read(StorePath);
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AgentTaskRecord>>(content) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void SaveAll(IEnumerable<AgentTaskRecord> tasks)
    {
        var ordered = tasks
            .OrderByDescending(task => task.UpdatedAt)
            .Take(200)
            .ToList();
        AtomicJsonFile.Write(StorePath, JsonSerializer.Serialize(ordered, JsonOptions));
    }
}
