using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace White_Desert.Services.Contracts;

public interface IGameSearchService
{

    public List<string> GetDrives();

    public List<string> SearchGamePathsInDrives(List<string> drives, IProgress<string>? progress);
    
    public Task<List<string>> SearchGamePathsInDrivesAsync(List<string> drives, IProgress<string>? progress);
    
    public bool IsGameDirectory(string path);
    
}