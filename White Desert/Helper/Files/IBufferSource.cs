using System;
using System.Threading;
using System.Threading.Tasks;

namespace White_Desert.Helper.Files;

public interface IBufferSource : IDisposable
{
    long Length { get; }
    
    byte GetByte(long offset);
    
    byte[] GetSlice(long offset, int length);
    
    void ReadInto(long offset, Span<byte> destination);
    
    ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken = default);
}