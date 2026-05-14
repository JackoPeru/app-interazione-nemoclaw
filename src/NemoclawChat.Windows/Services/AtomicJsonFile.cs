using System.Text;

namespace NemoclawChat_Windows.Services;

internal static class AtomicJsonFile
{
    public static void Write(string destinationPath, string content)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".tmp";
        var backupPath = destinationPath + ".bak";

        File.WriteAllText(tempPath, content, new UTF8Encoding(false));

        if (File.Exists(destinationPath))
        {
            try
            {
                File.Replace(tempPath, destinationPath, backupPath);
            }
            catch (IOException)
            {
                File.Copy(tempPath, destinationPath, overwrite: true);
                try { File.Delete(tempPath); } catch { }
            }
        }
        else
        {
            File.Move(tempPath, destinationPath);
        }
    }

    public static string? Read(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(sourcePath, new UTF8Encoding(false));
        }
        catch (IOException)
        {
            var backup = sourcePath + ".bak";
            if (File.Exists(backup))
            {
                try { return File.ReadAllText(backup, new UTF8Encoding(false)); } catch { return null; }
            }
            return null;
        }
    }
}
