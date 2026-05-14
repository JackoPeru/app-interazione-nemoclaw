using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
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

    private static string SettingsPath
    {
        get
        {
            return Path.Combine(DataDirectoryPath, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        var content = AtomicJsonFile.Read(SettingsPath);
        if (string.IsNullOrEmpty(content))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(content) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        AtomicJsonFile.Write(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static void Reset()
    {
        if (File.Exists(SettingsPath))
        {
            File.Delete(SettingsPath);
        }
    }
}
