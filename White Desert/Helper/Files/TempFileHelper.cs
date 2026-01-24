using System.Collections.Generic;
using System.IO;
using White_Desert.Models.GhostBridge;

namespace White_Desert.Helper.Files;

public static class TempFileHelper
{
    private static string GetTempDirectory() => Path.Combine(Path.GetTempPath(), "White Desert");

    private static string GetFileName(PazFile file) 
        => $"PAD{file.PazId:D5}_FID{file.FileId}_H{file.Hash}.tmp";

    public static string SaveTempFile(PazFile file, byte[] content, bool deleteOldCache)
    {
        var tempDirectory = GetTempDirectory();
        var tempFile = Path.Combine(tempDirectory, GetFileName(file));

        if (!Directory.Exists(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }
        
        if (File.Exists(tempFile))
        {
            if (new FileInfo(tempFile).Length == content.Length)
            {
                return tempFile;
            }
        }

        if (deleteOldCache)
        {
            CleanOldCacheForIndex(tempDirectory, (int)file.PazId);
        }

        using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);
        fs.Write(content, 0, content.Length);

        return tempFile;
    }

    public static bool ExistsTempFile(PazFile file, out string path)
    {
        var tempFile = Path.Combine(GetTempDirectory(), GetFileName(file));
        path = tempFile;
        return File.Exists(tempFile);
    }

    public static List<string> GetExistentTempFiles(PazFile file)
    {
        var tempDirectory = GetTempDirectory();
        if (!Directory.Exists(tempDirectory)) return [];

        var pattern = $"PAD{file.PazId:D5}_FID{file.FileId}_*.tmp";
        var tempFiles = Directory.GetFiles(tempDirectory, pattern);

        return [.. tempFiles];
    }

    private static void CleanOldCacheForIndex(string dir, int pazId)
    {
        try
        {
            var oldFiles = Directory.GetFiles(dir, $"PAD{pazId:D5}_FID*");
            foreach (var file in oldFiles)
            {
                try { File.Delete(file); } catch { /* Ignore */ }
            }
        }
        catch
        {
            /* Ignore */
        }
    }
}