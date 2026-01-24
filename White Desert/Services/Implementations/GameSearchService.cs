using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using White_Desert.Services.Contracts;

namespace White_Desert.Services.Implementations;

public class GameSearchService : IGameSearchService
{
    private readonly HashSet<string> _blacklistedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "ProgramData", "$Recycle.Bin", "System Volume Information", "Microsoft", "AppData"
    };

    private int _scanCount = 0;
    public ConcurrentBag<string> GamePaths { get; } = [];

    public List<string> GetDrives()
    {
        return DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed).Select(drive => drive.Name)
            .ToList();
    }

    public List<string> SearchGamePathsInDrives(List<string> drives, IProgress<string>? progress)
    {
        var topFolders = new List<DirectoryInfo>();

        GamePaths.Clear();
        
        foreach (var drive in drives)
        {
            try
            {
                var di = new DirectoryInfo(drive);
                if (!di.Exists) continue;

                foreach (var subDir in di.GetDirectories())
                {
                    if (_blacklistedFolders.Contains(subDir.Name)) continue;
                    if (subDir.Attributes.HasFlag(FileAttributes.Hidden)) continue;

                    topFolders.Add(subDir);
                }
            }
            catch (Exception)
            {
                /* Ignore */
            }
        }

        Parallel.ForEach(topFolders, folder =>
        {
            SearchRecursive(folder, "BlackDesertLauncher.exe", progress);
        });

        return GamePaths.ToList();
    }

    public async Task<List<string>> SearchGamePathsInDrivesAsync(List<string> drives, IProgress<string>? progress)
    {
        return await Task.Run(() => SearchGamePathsInDrives(drives, progress));
    }
    
    public bool IsGameDirectory(string? path)
    { 
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try 
        {
            var launcherPath = Path.Combine(path, "BlackDesertLauncher.exe");

            return File.Exists(launcherPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void SearchRecursive(DirectoryInfo directory, string targetExe, IProgress<string>? progress)
    {
        try
        {
            if (Interlocked.Increment(ref _scanCount) % 1000 == 0)
            {
                progress?.Report(directory.FullName);
            }
            
            var path = Path.Combine(directory.FullName, targetExe);
            if (File.Exists(path))
            {
                GamePaths.Add(directory.FullName);
            }

            foreach (var subDir in directory.EnumerateDirectories())
            {
                if ((subDir.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    continue;

                SearchRecursive(subDir, targetExe, progress);
            }
        }
        catch (UnauthorizedAccessException)
        {
            /* Ignore */
        }
        catch (DirectoryNotFoundException)
        {
            /* Ignore */
        }        
        catch (PathTooLongException)
        {
            /* Ignore */
        }
    }
}