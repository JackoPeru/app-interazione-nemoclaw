using System.Globalization;
using Windows.Storage;

namespace NemoclawChat_Windows.Services;

public sealed record VoicePreferences(
    string Voice = "if_sara",
    double Speed = 1.08,
    bool WakeWord = false,
    bool PushToTalk = false,
    bool ShowTranscript = false);

public static class VoicePreferencesStore
{
    public static readonly string[] SupportedVoices = ["if_sara", "im_nicola"];

    public static VoicePreferences Load(string? projectId)
    {
        var values = ApplicationData.Current.LocalSettings.Values;
        var key = Key(projectId);
        var raw = values[key] as string;
        if (string.IsNullOrWhiteSpace(raw) && !string.IsNullOrWhiteSpace(projectId))
        {
            raw = values[Key(string.Empty)] as string;
        }

        var parts = (raw ?? "if_sara|1.08|False|False|False").Split('|');
        var requestedVoice = parts.ElementAtOrDefault(0) ?? "if_sara";
        var voice = SupportedVoices.Contains(requestedVoice, StringComparer.OrdinalIgnoreCase)
            ? SupportedVoices.First(item => item.Equals(requestedVoice, StringComparison.OrdinalIgnoreCase))
            : "if_sara";
        var speed = double.TryParse(parts.ElementAtOrDefault(1), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSpeed)
            ? Math.Clamp(parsedSpeed, 0.75, 1.35)
            : 1.08;
        var preferences = new VoicePreferences(
            voice,
            speed,
            bool.TryParse(parts.ElementAtOrDefault(2), out var wake) && wake,
            bool.TryParse(parts.ElementAtOrDefault(3), out var pushToTalk) && pushToTalk,
            bool.TryParse(parts.ElementAtOrDefault(4), out var transcript) && transcript);

        if (!string.Equals(requestedVoice, voice, StringComparison.OrdinalIgnoreCase))
        {
            Save(projectId, preferences);
        }
        return preferences;
    }

    public static void Save(string? projectId, VoicePreferences preferences)
    {
        var voice = SupportedVoices.Contains(preferences.Voice, StringComparer.OrdinalIgnoreCase)
            ? preferences.Voice
            : "if_sara";
        var speed = Math.Clamp(preferences.Speed, 0.75, 1.35);
        ApplicationData.Current.LocalSettings.Values[Key(projectId)] = string.Join('|',
            voice,
            speed.ToString(CultureInfo.InvariantCulture),
            preferences.WakeWord,
            preferences.PushToTalk,
            preferences.ShowTranscript);
    }

    private static string Key(string? projectId) => $"VoiceProfile:{projectId?.Trim() ?? string.Empty}";
}
