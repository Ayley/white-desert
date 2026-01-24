using System;
using System.Collections.Generic;
using White_Desert.Models.GhostBridge;

namespace White_Desert.Services.Contracts;

public interface IPazService : IDisposable
{
    bool IsLoaded { get; }
    string GameDirectory { get; }
    
    ref readonly PazFile this[int index] { get; }
    
    void Initialize(string metaPath);
    
    List<FolderNameTuple> GetAllFolders();
    int[] GetFilesInFolder(uint targetFolderId);
    List<string> GetFileNames(int[] absoluteIndices);
    string GetFileName(uint fileId);
    string GetFolderPath(uint folderId);
    
    byte[]? GetFileBytes(PazFile entry);
    byte[]? DecompileLua(PazFile entry);
    int ExtractFilesBatch(string destinationRoot, List<uint> fileIndices, Helper.Interop.GhostBridge.ProgressCallback progressCallback);

    
    List<int> SearchFiles(string query);
    
    unsafe BdoIndex* GetRawIndex();
}