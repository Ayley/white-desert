using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace White_Desert.Helper.Files;

public class FileBufferSource : IBufferSource
{
    private readonly FileStream _stream;
    private readonly long _length;

    public FileBufferSource(string path)
    {
        _stream = new FileStream(path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.RandomAccess);

        _length = _stream.Length;
    }

    public long Length => _length;

    public byte GetByte(long offset)
    {
        Span<byte> buffer = stackalloc byte[1];
        RandomAccess.Read(_stream.SafeFileHandle, buffer, offset);
        return buffer[0];
    }

    public byte[] GetSlice(long offset, int length)
    {
        var actualLength = (int)Math.Min(length, _length - offset);
        if (actualLength <= 0) return Array.Empty<byte>();

        var buffer = new byte[actualLength];
        RandomAccess.Read(_stream.SafeFileHandle, buffer, offset);
        return buffer;
    }

    public void ReadInto(long offset, Span<byte> destination)
    {
        RandomAccess.Read(_stream.SafeFileHandle, destination, offset);
    }

    public async ValueTask<int> ReadAsync(long offset, Memory<byte> destination,
        CancellationToken cancellationToken = default)
    {
        return await RandomAccess.ReadAsync(_stream.SafeFileHandle, destination, offset, cancellationToken);
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}