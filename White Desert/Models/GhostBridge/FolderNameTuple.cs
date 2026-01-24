using System.Runtime.InteropServices;
using White_Desert.Models.GhostBridge.Rust;

namespace White_Desert.Models.GhostBridge;

[StructLayout(LayoutKind.Sequential)]
public struct FolderNameTuple
{
    public RustString FolderName; 
    public uint FolderIndex;
}