using System.Runtime.InteropServices;
using White_Desert.Models.GhostBridge.Rust;

namespace White_Desert.Models.GhostBridge;

[StructLayout(LayoutKind.Sequential)]
public struct PadMeta
{
    public uint Version;
    public uint PazFileCount;
    public RustVec<RustString> FileNames; 
    public RustVec<FolderNameTuple> FolderPaths;
}