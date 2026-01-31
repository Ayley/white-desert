using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace White_Desert.Services.Contracts;

public interface IGameSearchService
{
    public List<string> GetDrives();

    public void SearchGamePathsInDrives(
        List<string> drives,
        IProgress<string>? progress,
        CancellationToken token,
        Action<string> onPathFound);

    public Task SearchGamePathsInDrivesAsync(
        List<string> drives,
        IProgress<string>? progress,
        CancellationToken token,
        Action<string> onPathFound);

    public bool IsGameDirectory(string path);
}