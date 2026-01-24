using System;
using System.Runtime.InteropServices;

namespace White_Desert.Models.GhostBridge.Rust;

[StructLayout(LayoutKind.Sequential)]
public struct RustVec<T> where T : unmanaged
{
    public IntPtr Data;
    public UIntPtr Len;
    public UIntPtr Capacity;
    
    public unsafe ReadOnlySpan<T> AsSpan()
    {
        if (Data == IntPtr.Zero || Len == UIntPtr.Zero)
            return ReadOnlySpan<T>.Empty;

        return new ReadOnlySpan<T>((void*)Data, (int)Len);
    }
    
    public static RustVec<T> FromArray(T[] array)
    {
        return new RustVec<T>
        {
            Data = IntPtr.Zero,
            Len = (nuint)array.Length,
            Capacity = (nuint)array.Length
        };
    }
}