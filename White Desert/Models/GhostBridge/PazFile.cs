using System.Runtime.InteropServices;

namespace White_Desert.Models.GhostBridge;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PazFile
{
    public uint Hash { get; set; }
    public uint FolderId { get; set; }
    public uint FileId { get; set; }
    public uint PazId { get; set; }
    public uint Offset { get; set; }
    public uint CompressedSize { get; set; }
    public uint OriginalSize { get; set; }
}