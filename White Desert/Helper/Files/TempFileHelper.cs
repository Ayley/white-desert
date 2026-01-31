using System.Collections.Generic;
using System.IO;
using White_Desert.Models.GhostBridge;

namespace White_Desert.Helper.Files;

public static class TempFileHelper
{
    private static string GetTempDirectory() => Path.Combine(Path.GetTempPath(), "White Desert");

    private static string GetFileName(PazFile file, string typeTag = "raw")
        => $"PAD{file.PazId:D5}_FID{file.FileId}_H{file.Hash}_{typeTag}.cache";


    public static string SaveProcessedFile(PazFile file, byte[] content, string typeTag, bool deleteOldCache)
    {
        var tempDirectory = GetTempDirectory();
        var tempFile = Path.Combine(tempDirectory, GetFileName(file, typeTag));


        if (!Directory.Exists(tempDirectory))
            Directory.CreateDirectory(tempDirectory);

        if (File.Exists(tempFile) && new FileInfo(tempFile).Length == content.Length)
            return tempFile;

        if (deleteOldCache)
            CleanOldCacheForIndex(tempDirectory, file, typeTag);

        using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
            FileOptions.SequentialScan);
        fs.Write(content, 0, content.Length);


        return tempFile;
    }

    public static bool TryGetProcessedFile(PazFile file, string typeTag, out string path)
    {
        path = Path.Combine(GetTempDirectory(), GetFileName(file, typeTag));
        return File.Exists(path);
    }

    public static List<string> GetExistentTempFiles(PazFile file)
    {
        var tempDirectory = GetTempDirectory();
        if (!Directory.Exists(tempDirectory)) return [];

        var pattern = $"PAD{file.PazId:D5}_FID{file.FileId}_*.cache";

        return [.. Directory.GetFiles(tempDirectory, pattern)];
    }

    public static void CleanAllCache()
    {
        var dir = GetTempDirectory();
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                /* Ignore */
            }
        }
    }

    private static void CleanOldCacheForIndex(string dir, PazFile file, string typeTag)
    {
        try
        {
            var pattern = $"PAD{file.PazId:D5}_FID{file.FileId}_H*_{typeTag}.cache";

            var oldFiles = Directory.GetFiles(dir, pattern);
            foreach (var f in oldFiles)
            {
                if (!f.Contains($"_H{file.Hash}_"))
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch
                    {
                        /* Ignore */
                    }
                }
            }
        }
        catch
        {
            /* Ignore */
        }
    }
}