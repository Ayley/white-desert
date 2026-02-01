using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using SkiaSharp;
using White_Desert.Helper;
using White_Desert.Helper.Files;
using White_Desert.Helper.Image;
using White_Desert.Messages;
using White_Desert.Models;
using White_Desert.Services.Contracts;

namespace White_Desert.ViewModels.Editor;

public partial class ImageEditorViewModel : ViewModelBase
{
    [ObservableProperty] private Bitmap? _selectedImage;

    private readonly IFileService<AppSettings> _fileService;

    private CancellationTokenSource? _tokenSource;


    public ImageEditorViewModel(IFileService<AppSettings> fileService)
    {
        _fileService = fileService;

        RegisterWeakReferences();
    }

    private void RegisterWeakReferences()
    {
        WeakReferenceMessenger.Default.Register<SelectedFileChanged>(this, SelectedFileChanged);
    }

    private async void SelectedFileChanged(object recipient, SelectedFileChanged message)
    {
        _tokenSource?.Cancel();
        _tokenSource?.Dispose();
        
        
        _tokenSource = new CancellationTokenSource();
        var token = _tokenSource.Token;

        try
        {
            if (message.Node.IsFolder) return;
            if (!_fileService.Data!.ShowImageView) return;
            if (!message.File.HasValue) return;
            if (message.Content == null || message.Content.Length == 0) return;

            var imageType = ImageHelper.GetImageType(message.Node.Name.ToLower());

            if (imageType == ImageType.None) return;

            byte[]? imageContent = null;

            if (TempFileHelper.TryGetProcessedFile(message.File.Value, "image", out var path))
            {
                imageContent = await File.ReadAllBytesAsync(path, token);
            }
            else
            {
                if (imageType == ImageType.Standard)
                {
                    imageContent = message.Content;
                }
                else if (imageType == ImageType.Dds)
                {
                    imageContent = await ImageHelper.ConvertDdsImageAsync(message.Content);
                }

                if (imageContent != null)
                {
                    var bytesToCache = imageContent;
                    _ = Task.Run(() => TempFileHelper.SaveProcessedFile(
                        message.File.Value, bytesToCache, "image", _fileService.Data!.DeleteOldCachedFiles), token);
                }
            }

            if (imageContent != null && !token.IsCancellationRequested)
            {
                var bitmap = await Task.Run(() =>
                {
                    using var ms = new MemoryStream(imageContent);
                    return Bitmap.DecodeToHeight(ms, 1080);
                }, token);

                var oldImage = SelectedImage;
                
                SelectedImage = bitmap;
                
                oldImage?.Dispose();
            }
            else
            {
                _tokenSource?.Dispose();
                _tokenSource = null;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error loading image for {FileName}", message.Node.Name);
        }
    }
}