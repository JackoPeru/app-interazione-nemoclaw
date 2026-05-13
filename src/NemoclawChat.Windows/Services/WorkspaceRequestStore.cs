using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed class WorkspaceRequestRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = "Nuova richiesta";
    public string Prompt { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public static class WorkspaceRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string CurrentDirectoryName = "ChatClaw";

    private static string StorePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(localAppData, CurrentDirectoryName);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "workspace-requests.json");
        }
    }

    public static List<WorkspaceRequestRecord> Load()
    {
        if (!File.Exists(StorePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkspaceRequestRecord>>(File.ReadAllText(StorePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static IReadOnlyList<WorkspaceRequestRecord> Recent(string kind, int count = 8)
    {
        return Load()
            .Where(item => item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .Take(count)
            .ToList();
    }

    public static WorkspaceRequestRecord Save(string kind, string prompt, string result, string source, string status)
    {
        var items = Load();
        var record = new WorkspaceRequestRecord
        {
            Kind = kind,
            Title = MakeTitle(prompt),
            Prompt = prompt,
            Result = result,
            Source = source,
            Status = status,
            UpdatedAt = DateTimeOffset.Now
        };
        items.Insert(0, record);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(items.Take(200).ToList(), JsonOptions));
        return record;
    }

    private static string MakeTitle(string prompt)
    {
        var oneLine = prompt.ReplaceLineEndings(" ").Trim();
        return oneLine.Length <= 54 ? oneLine : oneLine[..54].TrimEnd() + "...";
    }
}
