namespace NemoclawChat_Windows.Services;

public sealed class LocalVideoRecord
{
    public string Path { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public DateTimeOffset ModifiedAt { get; init; }
    public long SizeBytes { get; init; }
    public string LastFeedback { get; init; } = string.Empty;
    public string LastAgentStatus { get; init; } = string.Empty;
    public string LastAgentResponse { get; init; } = string.Empty;
    public int FeedbackCount { get; init; }
    public string ModifiedLabel => $"Aggiornato {ModifiedAt.LocalDateTime:g}";
    public string SizeLabel => $"Peso {VideoLibraryService.FormatSize(SizeBytes)}";
    public string FeedbackCountLabel => FeedbackCount == 0 ? "Nessun feedback" : $"{FeedbackCount} feedback";
    public string FeedbackPreview => string.IsNullOrWhiteSpace(LastFeedback) ? "Nessun feedback inviato." : $"Feedback: {LastFeedback}";
}

public static class VideoLibraryService
{
    private static readonly string[] SupportedExtensions =
    [
        ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi"
    ];

    public static string GetLibraryPath(AppSettings settings)
    {
        return settings.VideoLibraryPath.Trim();
    }

    public static string EnsureLibraryPath(AppSettings settings)
    {
        var path = GetLibraryPath(settings);
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }
        Directory.CreateDirectory(path);
        return path;
    }

    public static IReadOnlyList<LocalVideoRecord> Scan(AppSettings settings)
    {
        var folder = EnsureLibraryPath(settings);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return [];
        }
        var feedbackByPath = VideoFeedbackStore.Load()
            .GroupBy(item => item.VideoPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);

        return new DirectoryInfo(folder)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file => SupportedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file =>
            {
                feedbackByPath.TryGetValue(file.FullName, out var feedback);
                return new LocalVideoRecord
                {
                    Path = file.FullName,
                    FileName = file.Name,
                    Title = Path.GetFileNameWithoutExtension(file.Name).Replace('_', ' ').Replace('-', ' ').Trim(),
                    Extension = file.Extension.TrimStart('.').ToUpperInvariant(),
                    ModifiedAt = new DateTimeOffset(file.LastWriteTimeUtc),
                    SizeBytes = file.Length,
                    LastFeedback = feedback?.Feedback ?? string.Empty,
                    LastAgentStatus = feedback?.AgentStatus ?? string.Empty,
                    LastAgentResponse = feedback?.AgentResponse ?? string.Empty,
                    FeedbackCount = feedback?.FeedbackCount ?? 0
                };
            })
            .ToList();
    }

    public static string FormatSize(long sizeBytes)
    {
        var size = sizeBytes;
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;
        double value = size;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }
}
