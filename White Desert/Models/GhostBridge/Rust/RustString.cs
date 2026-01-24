using System;
using System.Runtime.InteropServices;

namespace White_Desert.Models.GhostBridge.Rust;

[StructLayout(LayoutKind.Sequential)]
public struct RustString
{
    public IntPtr Data;
    public UIntPtr Len;
    public UIntPtr Capacity;
    
    public override string ToString()
    {
        if (Data == IntPtr.Zero || Len == UIntPtr.Zero) 
            return string.Empty;
        
        return Marshal.PtrToStringUTF8(Data, (int)Len) ?? string.Empty;
    }
}