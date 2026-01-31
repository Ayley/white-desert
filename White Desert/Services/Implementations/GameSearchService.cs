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
        "Windows", 
        "ProgramData", 
        "System Volume Information", 
        "$Recycle.Bin", 
        "Boot", 
        "Recovery", 
        "PerfLogs",
        "Config.Msi",
        "MSOCache",
        "Documents and Settings", 
        "System",

        "AppData", 
        "Microsoft", 
        "Application Data", 
        "Local Settings",  
        "NetHood",
        "PrintHood",
        "Cookies",
        "Recent",
        "Temp",

        "Intel", 
        "AMD", 
        "NVIDIA", 
        "Drivers",
        "Dell",  
        "HP",    
    
        "OneDriveTemp" 
    };

    private int _scanCount = 0;
    public ConcurrentBag<string> GamePaths { get; } = [];

    public List<string> GetDrives()
    {
        return DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed).Select(drive => drive.Name)
            .ToList();
    }

    public void SearchGamePathsInDrives(
        List<string> drives,
        IProgress<string>? progress,
        CancellationToken token,
        Action<string> onPathFound)
    {
        var topFolders = new List<DirectoryInfo>();
        GamePaths.Clear();
        _scanCount = 0;

        foreach (var drive in drives)
        {
            if (token.IsCancellationRequested) return;

            try
            {
                var di = new DirectoryInfo(drive);
                if (!di.Exists) continue;

                foreach (var subDir in di.GetDirectories())
                {
                    if (_blacklistedFolders.Contains(subDir.Name)) continue;
                    if (subDir.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                    if (subDir.Attributes.HasFlag(FileAttributes.System)) continue;

                    topFolders.Add(subDir);
                }

                CheckDirectory(di, "BlackDesertLauncher.exe", onPathFound);
            }
            catch
            {
                /* Ignore */
            }
        }

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        try
        {
            Parallel.ForEach(topFolders, parallelOptions,
                folder => { SearchRecursive(folder, "BlackDesertLauncher.exe", onPathFound, progress, token); });
        }
        catch (OperationCanceledException)
        {
            /* Ignore */
        }
    }

    public async Task SearchGamePathsInDrivesAsync(
        List<string> drives,
        IProgress<string>? progress,
        CancellationToken token,
        Action<string> onPathFound)
    {
        await Task.Run(() => SearchGamePathsInDrives(drives, progress, token, onPathFound), token);
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

    private void SearchRecursive(
        DirectoryInfo directory,
        string targetExe,
        Action<string> onPathFound,
        IProgress<string>? progress,
        CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        try
        {
            if (Interlocked.Increment(ref _scanCount) % 100 == 0)
            {
                progress?.Report(directory.FullName);
            }

            CheckDirectory(directory, targetExe, onPathFound);

            foreach (var subDir in directory.EnumerateDirectories())
            {
                if (token.IsCancellationRequested) return;

                if ((subDir.Attributes &
                     (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0)
                    continue;

                SearchRecursive(subDir, targetExe, onPathFound, progress, token);
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
        catch (Exception)
        {
            /* Ignore */
        }
    }

    private void CheckDirectory(DirectoryInfo directory, string targetExe, Action<string> onPathFound)
    {
        var fullPath = Path.Combine(directory.FullName, targetExe);
        if (File.Exists(fullPath))
        {
            GamePaths.Add(directory.FullName);

            onPathFound?.Invoke(directory.FullName);
        }
    }
}