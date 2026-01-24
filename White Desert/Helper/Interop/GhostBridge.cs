using System;
using System.Runtime.InteropServices;
using White_Desert.Models.GhostBridge;
using White_Desert.Models.GhostBridge.Rust;

namespace White_Desert.Helper.Interop;

public static partial class GhostBridge
{
    private const string DllName = "black_ghost.dll";

    public delegate void ProgressCallback(int current, int total);
    
    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr load_bdo_index(string path);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial RustVec<byte> get_file_content(string pazFolderPath, PazFile fileInfo, string fileName);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial RustVec<byte> decompile_lua(string pazFolderPath, PazFile fileInfo, string fileName);

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nuint extract_files_batch(
        string saveFolder, 
        string pazFolderPath, 
        RustVec<uint> fileIndices, 
        IntPtr bdoIndexHandle,
        ProgressCallback progressCallback
    );
    
    [LibraryImport(DllName)]
    public static partial void free_bdo_index(IntPtr index);

    [LibraryImport(DllName)]
    public static partial void free_file_content(RustVec<byte> vec);
}