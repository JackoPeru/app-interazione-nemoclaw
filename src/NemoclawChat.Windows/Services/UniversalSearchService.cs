namespace NemoclawChat_Windows.Services;

public sealed record UniversalSearchResult(string Kind, string Id, string Title, string Snippet, string ProjectId, DateTimeOffset UpdatedAt);

public static class UniversalSearchService
{
    public static async Task<IReadOnlyList<UniversalSearchResult>> SearchAsync(string query, AppSettings settings)
    {
        var needle = query.Trim();
        if (needle.Length < 2) return [];
        var results = new List<UniversalSearchResult>();
        foreach (var item in ChatArchiveStore.Load())
        {
            var body = string.Join('\n', new[]
            {
                item.Title, item.Description, item.Prompt, item.ProjectMemory, item.ProjectInstructions,
                item.WorkspacePath, item.RepositoryUrl, item.ArtifactFileName, string.Join(' ', item.Tags),
                string.Join('\n', item.Messages.Select(message => message.Text)),
                string.Join('\n', item.Messages.SelectMany(message => message.RawEvents ?? []).Select(raw => raw.Json)),
                string.Join('\n', item.Messages.SelectMany(message => message.VisualBlocks ?? []).Select(block => $"{block.Title} {block.Caption} {block.Text} {block.Code} {block.Filename}"))
            });
            if (body.Contains(needle, StringComparison.OrdinalIgnoreCase))
                results.Add(new(item.Kind, item.Id, item.Title, Snippet(body, needle), item.ProjectId, item.UpdatedAt));
        }

        foreach (var task in AgentTaskStore.Load().Where(task => $"{task.Title} {task.Detail} {task.Status} {task.Source}".Contains(needle, StringComparison.OrdinalIgnoreCase)))
            results.Add(new("Run", task.Id, task.Title, task.Detail, string.Empty, task.UpdatedAt));

        var memoryTask = GatewayService.LoadHubMemoryAsync(settings);
        var cronTask = GatewayService.LoadCronJobsAsync(settings, true);
        var notificationTask = GatewayService.LoadHubNotificationsAsync(settings, false);
        await Task.WhenAll(memoryTask, cronTask, notificationTask);
        var memory = memoryTask.Result.Memory;
        var memoryText = $"{memory.VideoPreferences} {memory.NewsPreferences} {memory.ResponseStyle} {memory.ProjectRules} {memory.GeneralNotes}";
        if (memoryText.Contains(needle, StringComparison.OrdinalIgnoreCase)) results.Add(new("Memoria", "hub-memory", "Memoria Hermes", Snippet(memoryText, needle), settings.ActiveProjectId, DateTimeOffset.Now));
        foreach (var job in cronTask.Result.Jobs.Where(job => $"{job.Name} {job.Prompt} {job.Schedule} {job.LastStatus}".Contains(needle, StringComparison.OrdinalIgnoreCase)))
            results.Add(new("Cron", job.Id, job.Name, Snippet($"{job.Prompt} {job.LastStatus}", needle), string.Empty, DateTimeOffset.Now));
        foreach (var note in notificationTask.Result.Items.Where(note => $"{note.Title} {note.Message} {note.Source}".Contains(needle, StringComparison.OrdinalIgnoreCase)))
            results.Add(new("Notifica", note.Id, note.Title, Snippet(note.Message, needle), string.Empty, note.CreatedAt));
        return results.OrderByDescending(result => result.UpdatedAt).Take(250).ToList();
    }

    private static string Snippet(string text, string needle)
    {
        var clean = text.ReplaceLineEndings(" ").Trim();
        var index = clean.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, index - 70);
        var length = Math.Min(240, clean.Length - start);
        return clean.Substring(start, length);
    }
}
