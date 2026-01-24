using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using White_Desert.Models.GhostBridge;

namespace White_Desert.Models;

public class PazFileInfo
{
    public PazFile RawData { get; set; }

    public string FileName { get; set; }
    public string FolderName { get; set; }
    public ObservableCollection<string> cachedFilePaths { get; set; }
    
    public uint Hash => RawData.Hash;
    public uint FolderId => RawData.FolderId;
    public uint FileId => RawData.FileId;
    public uint PazId => RawData.PazId;
    public uint Offset => RawData.Offset;
    public uint CompressedSize => RawData.CompressedSize;
    public uint OriginalSize => RawData.OriginalSize;

    public bool IsCompressed => OriginalSize != CompressedSize;
    
    public string PazFileName => $"PAD{PazId:D5}.PAZ";
    
    public double CompressionRatio => OriginalSize > 0 
        ? (1.0 - (double)CompressedSize / OriginalSize) * 100 
        : 0;

    public string CompressionRatioString => IsCompressed 
        ? $"{CompressionRatio:F1}% saved" 
        : "Raw / Store";

    public string FormattedOriginalSize => FormatBytes(OriginalSize);
    public string MappingString => $"FolderID: {FolderId} | FileID: {FileId}";

    private static string FormatBytes(uint bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB" };
        double dblSByte = bytes;
        int i;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }
        return $"{dblSByte:0.##} {suffix[i]}";
    }
}