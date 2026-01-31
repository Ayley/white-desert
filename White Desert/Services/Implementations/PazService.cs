using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serilog;
using White_Desert.Helper.Interop;
using White_Desert.Models.GhostBridge;
using White_Desert.Models.GhostBridge.Rust;
using White_Desert.Services.Contracts;

namespace White_Desert.Services.Implementations;

public unsafe class PazService : IPazService
{
    private readonly Lock _lock = new();
    private IntPtr _indexHandle = IntPtr.Zero;

    public BdoIndex* Index { get; private set; } = null;
    public string GameDirectory { get; private set; } = string.Empty;
    public string PazDirectory => string.IsNullOrEmpty(GameDirectory) ? "" : GameDirectory + @"\Paz";
    public bool IsLoaded => _indexHandle != IntPtr.Zero && Index != null;

    public BdoIndex* GetRawIndex() => Index;

    public ref readonly PazFile this[int index]
    {
        get
        {
            lock (_lock)
            {
                if (!IsLoaded || index < 0 || index >= (int)Index->PazFiles.Len)
                    throw new IndexOutOfRangeException($"Index {index} is out of Array.");

                PazFile* data = (PazFile*)Index->PazFiles.Data;
                return ref data[index];
            }
        }
    }
    
    public void Initialize(string metaPath)
    {
        lock (_lock)
        {
            Dispose(); 

            try 
            {
                GameDirectory = metaPath;
                string fullPath = GameDirectory + @"\Paz\pad00000.meta";
                
                _indexHandle = GhostBridge.load_bdo_index(fullPath);

                if (_indexHandle == IntPtr.Zero)
                    throw new Exception("Cannot load Paz File");

                Index = (BdoIndex*)_indexHandle;
            }
            catch (Exception ex)
            {
                Log.Information(ex.Message);
                throw;
            }
        }
    }

    public List<FolderNameTuple> GetAllFolders()
    {
        lock (_lock)
        {
            if (!IsLoaded) return [];
            var span = Index->Metadata.FolderPaths.AsSpan();
            return [.. span.ToArray()];
        }
    }

    public int[] GetFilesInFolder(uint targetFolderId)
    {
        lock (_lock)
        {
            if (!IsLoaded) return Array.Empty<int>();

            var absoluteIndices = new List<int>();
            var entries = Index->PazFiles.AsSpan();

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].FolderId == targetFolderId)
                    absoluteIndices.Add(i);
            }
            return absoluteIndices.ToArray();
        }
    }

    public List<string> GetFileNames(int[] absoluteIndices)
    {
        lock (_lock)
        {
            if (!IsLoaded) return [];

            var allNames = Index->Metadata.FileNames.AsSpan();
            var allEntries = Index->PazFiles.AsSpan();
            var result = new List<string>(absoluteIndices.Length);

            foreach (int i in absoluteIndices)
            {
                var entry = allEntries[i];
                int nameIdx = (int)entry.FileId;

                if (nameIdx >= 0 && nameIdx < allNames.Length)
                    result.Add(allNames[nameIdx].ToString());
                else
                    result.Add("Unknown_File");
            }
            return result;
        }
    }

    public string GetFileName(uint fileId)
    {
        lock (_lock)
        {
            if (!IsLoaded) return "Unknown";
            var allNames = Index->Metadata.FileNames.AsSpan();
            return fileId < (uint)allNames.Length ? allNames[(int)fileId].ToString() : "Unknown_File";
        }
    }

    public string GetFolderPath(uint folderId)
    {
        lock (_lock)
        {
            if (!IsLoaded) return "";
            var allFolders = Index->Metadata.FolderPaths.AsSpan();
            return folderId < (uint)allFolders.Length ? allFolders[(int)folderId].FolderName.ToString() : "";
        }
    }

    public byte[]? GetFileBytes(PazFile entry)
    {
        lock (_lock)
        {
            if (!IsLoaded) return null;
            var rustVec = GhostBridge.get_file_content(PazDirectory, entry);
            if (rustVec.Data == IntPtr.Zero) return null;

            try { return rustVec.AsSpan().ToArray(); }
            finally { GhostBridge.free_file_content(rustVec); }
        }
    }

    public byte[]? DecompileLua(PazFile entry)
    {
        lock (_lock)
        {
            if (!IsLoaded) return null;
            var rustVec = GhostBridge.decompile_lua(PazDirectory, entry);
            if (rustVec.Data == IntPtr.Zero) return null;

            try { return rustVec.AsSpan().ToArray(); }
            finally { GhostBridge.free_file_content(rustVec); }
        }
    }

    public int ExtractFilesBatch(string destinationRoot, List<uint> fileIndices, ExtractType extractType, GhostBridge.ProgressCallback progressCallback)
    {
        if (fileIndices.Count == 0) return 0;

        uint[] indicesArray = fileIndices.ToArray();

        lock (_lock)
        {
            if (!IsLoaded) return 0;

            fixed (uint* pIndices = indicesArray)
            {
                var rustIndices = new RustVec<uint>
                {
                    Data = (IntPtr)pIndices,
                    Len = (nuint)indicesArray.Length,
                    Capacity = (nuint)indicesArray.Length
                };

                try
                {
                    var result = GhostBridge.extract_files_batch(
                        destinationRoot,
                        PazDirectory,
                        rustIndices,
                        _indexHandle,
                        extractType,
                        progressCallback
                    );

                    return (int)result;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error extracting files");
                    throw;
                }
            }
        }
    }
    
    public List<int> SearchFiles(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !IsLoaded) return [];
        
        BdoIndex* localIndex;
        lock (_lock) { localIndex = Index; }

        if (localIndex == null) return [];

        var pazFilesPtr = localIndex->PazFiles.Data;
        var namesPtr = localIndex->Metadata.FileNames.Data;
        var totalCount = (int)localIndex->PazFiles.Len;

        return Enumerable.Range(0, totalCount)
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .Where(i =>
            {
                var entry = ((PazFile*)pazFilesPtr)[i];
                var rustName = ((RustString*)namesPtr)[(int)entry.FileId];
                return rustName.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_indexHandle != IntPtr.Zero)
            {
                GhostBridge.free_bdo_index(_indexHandle);
                _indexHandle = IntPtr.Zero;
                Index = null;
            }
        }
        GC.SuppressFinalize(this);
    }

    ~PazService() => Dispose();
}