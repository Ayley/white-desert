using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DirectXTexNet;
using PixelFormat = Avalonia.Platform.PixelFormat;

namespace White_Desert.Helper.Image;

public static class ImageHelper
{
    public static async Task<byte[]?> ConvertDdsImageAsync(byte[] ddsContent)
    {
        return await Task.Run(() =>
        {
            unsafe
            {
                fixed (byte* pDds = ddsContent)
                {
                    try
                    {
                        using var scratch =
                            TexHelper.Instance.LoadFromDDSMemory((nint)pDds, ddsContent.Length, DDS_FLAGS.NONE);
                        var meta = scratch.GetMetadata();
                        var isCompressed = TexHelper.Instance.IsCompressed(meta.Format);

                        using var codec = isCompressed
                            ? scratch.Decompress(DXGI_FORMAT.B8G8R8A8_UNORM)
                            : scratch;

                        var img = codec.GetImage(0, 0, 0);

                        var bitmap = new WriteableBitmap(
                            new PixelSize(meta.Width, meta.Height),
                            new Vector(96, 96),
                            PixelFormat.Bgra8888,
                            AlphaFormat.Premul);

                        using (var buffer = bitmap.Lock())
                        {
                            var height = meta.Height;
                            var rowBytes = buffer.RowBytes;
                            var imgRowPitch = (int)img.RowPitch;
                            var srcBase = img.Pixels;
                            var destBase = buffer.Address;

                            Parallel.For(0, height, y =>
                            {
                                var src = (void*)(srcBase + (y * imgRowPitch));
                                var dest = (void*)(destBase + (y * rowBytes));
                                Buffer.MemoryCopy(src, dest, rowBytes, Math.Min(imgRowPitch, rowBytes));
                            });
                        }

                        using var ms = new MemoryStream();
                        bitmap.Save(ms);
                        return ms.ToArray();
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        });
    }

    public static ImageType GetImageType(string fileName)
    {
        var ext = Path.GetExtension(fileName.AsSpan());

        if (ext.Equals(".dds", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".dds1", StringComparison.OrdinalIgnoreCase))
        {
            return ImageType.Dds;
        }

        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return ImageType.Standard;
        }

        return ImageType.None;
    }
}