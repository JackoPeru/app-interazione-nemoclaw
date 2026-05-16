using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed class VideoFeedbackRecord
{
    public string VideoPath { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public string AgentStatus { get; set; } = string.Empty;
    public string AgentResponse { get; set; } = string.Empty;
    public int FeedbackCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public static class VideoFeedbackStore
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
            return Path.Combine(directory, "video-feedback.json");
        }
    }

    public static List<VideoFeedbackRecord> Load()
    {
        var content = AtomicJsonFile.Read(StorePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<VideoFeedbackRecord>>(content) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void Save(string videoPath, string videoTitle, string feedback, string agentStatus, string agentResponse)
    {
        var items = Load();
        var existing = items.FirstOrDefault(item => item.VideoPath.Equals(videoPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            items.Add(new VideoFeedbackRecord
            {
                VideoPath = videoPath,
                VideoTitle = videoTitle,
                Feedback = feedback,
                AgentStatus = agentStatus,
                AgentResponse = agentResponse,
                FeedbackCount = 1,
                UpdatedAt = DateTimeOffset.Now
            });
        }
        else
        {
            existing.VideoTitle = videoTitle;
            existing.Feedback = feedback;
            existing.AgentStatus = agentStatus;
            existing.AgentResponse = agentResponse;
            existing.FeedbackCount++;
            existing.UpdatedAt = DateTimeOffset.Now;
        }

        AtomicJsonFile.Write(StorePath, JsonSerializer.Serialize(items.OrderByDescending(item => item.UpdatedAt).ToList(), JsonOptions));
    }
}
