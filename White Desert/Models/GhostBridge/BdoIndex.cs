using System.Runtime.InteropServices;
using White_Desert.Models.GhostBridge.Rust;

namespace White_Desert.Models.GhostBridge;

[StructLayout(LayoutKind.Sequential)]
public struct BdoIndex
{
    public PadMeta Metadata;
    public RustVec<PazFile> PazFiles;
}