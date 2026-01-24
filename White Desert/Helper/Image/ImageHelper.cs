using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DirectXTexNet;
using White_Desert.Helper.Image;

namespace White_Desert.Helper;

public static class ImageHelper
{
    public static Bitmap? ConvertPngImage(byte[] content)
    {
        try
        {
            using var ms = new MemoryStream(content, 0, content.Length);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    public static unsafe Bitmap? ConvertDdsImage(byte[] content)
    {
        fixed (byte* pDds = content)
        {
            try
            {
                using var scratch = TexHelper.Instance.LoadFromDDSMemory((nint)pDds, content.Length, DDS_FLAGS.NONE);
                var meta = scratch.GetMetadata();

                var isCompressed = TexHelper.Instance.IsCompressed(meta.Format);
                using var codec = isCompressed
                    ? scratch.Decompress(DXGI_FORMAT.B8G8R8A8_UNORM)
                    : scratch;

                var img = codec.GetImage(0, 0, 0);
                var bitmap = new WriteableBitmap(new PixelSize(meta.Width, meta.Height), new Vector(96, 96),
                    PixelFormat.Bgra8888, AlphaFormat.Premul);

                using var buffer = bitmap.Lock();
                for (var y = 0; y < meta.Height; y++)
                {
                    var src = (void*)(img.Pixels + (y * (int)img.RowPitch));
                    var dest = (void*)(buffer.Address + (y * buffer.RowBytes));

                    Buffer.MemoryCopy(src, dest, buffer.RowBytes, Math.Min(img.RowPitch, buffer.RowBytes));
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }
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