using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record ChatMessageRecord(string Author, string Text, DateTimeOffset Timestamp);

public sealed class ConversationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Nuova chat";
    public string Kind { get; set; } = "Chat";
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<ChatMessageRecord> Messages { get; set; } = [];
}

public sealed record HomeNavigationRequest(string? ConversationId = null, string? Prompt = null);

public static class ChatArchiveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static event Action? Changed;
    private const string CurrentDirectoryName = "ChatClaw";
    private const string LegacyDirectoryName = "NemoclawChat";

    private static string DataDirectoryPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var currentDirectory = Path.Combine(localAppData, CurrentDirectoryName);
            var legacyDirectory = Path.Combine(localAppData, LegacyDirectoryName);

            if (!Directory.Exists(currentDirectory) && Directory.Exists(legacyDirectory))
            {
                Directory.CreateDirectory(currentDirectory);
                foreach (var file in Directory.GetFiles(legacyDirectory))
                {
                    var destination = Path.Combine(currentDirectory, Path.GetFileName(file));
                    if (!File.Exists(destination))
                    {
                        File.Copy(file, destination);
                    }
                }
            }

            Directory.CreateDirectory(currentDirectory);
            return currentDirectory;
        }
    }

    private static string StorePath
    {
        get
        {
            return Path.Combine(DataDirectoryPath, "conversations.json");
        }
    }

    public static List<ConversationRecord> Load()
    {
        if (!File.Exists(StorePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ConversationRecord>>(File.ReadAllText(StorePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static ConversationRecord? Find(string id)
    {
        return Load().FirstOrDefault(item => item.Id == id);
    }

    public static ConversationRecord SaveExchange(
        string? conversationId,
        string mode,
        string prompt,
        string response)
    {
        var items = Load();
        var conversation = string.IsNullOrWhiteSpace(conversationId)
            ? null
            : items.FirstOrDefault(item => item.Id == conversationId);

        if (conversation is null)
        {
            conversation = new ConversationRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = MakeTitle(prompt),
                Kind = mode == "Agente" ? "Task" : "Chat",
                Description = mode == "Agente"
                    ? "Conversazione agente con approve/deny demo."
                    : "Conversazione chat locale.",
                Prompt = prompt
            };
            items.Insert(0, conversation);
        }

        conversation.Kind = mode == "Agente" ? "Task" : conversation.Kind;
        conversation.Prompt = prompt;
        conversation.UpdatedAt = DateTimeOffset.Now;
        conversation.Messages.Add(new ChatMessageRecord("Tu", prompt, DateTimeOffset.Now));
        conversation.Messages.Add(new ChatMessageRecord("OpenClaw", response, DateTimeOffset.Now));
        SaveAll(items);
        return conversation;
    }

    public static ConversationRecord SaveProject(string title, string description, string prompt)
    {
        var items = Load();
        var existing = items.FirstOrDefault(item =>
            item.Kind == "Progetto" &&
            string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new ConversationRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = "Progetto",
                Title = title,
                Description = description,
                Prompt = prompt
            };
            items.Insert(0, existing);
        }

        existing.Description = description;
        existing.Prompt = prompt;
        existing.UpdatedAt = DateTimeOffset.Now;
        SaveAll(items);
        return existing;
    }

    public static IReadOnlyList<ConversationRecord> Recent(int count)
    {
        return Load()
            .OrderByDescending(item => item.UpdatedAt)
            .Take(count)
            .ToList();
    }

    private static void SaveAll(List<ConversationRecord> items)
    {
        var ordered = items
            .OrderByDescending(item => item.UpdatedAt)
            .Take(200)
            .ToList();
        File.WriteAllText(StorePath, JsonSerializer.Serialize(ordered, JsonOptions));
        Changed?.Invoke();
    }

    private static string MakeTitle(string prompt)
    {
        var oneLine = prompt.ReplaceLineEndings(" ").Trim();
        if (oneLine.Length <= 46)
        {
            return oneLine;
        }

        return oneLine[..46].TrimEnd() + "...";
    }
}
