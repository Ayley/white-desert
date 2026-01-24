using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
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
    
    public ImageEditorViewModel(IFileService<AppSettings> fileService)
    {
        _fileService = fileService;
        
        RegisterWeakReferences();
    }
    
    private void RegisterWeakReferences()
    {
        WeakReferenceMessenger.Default.Register<SelectedFileChanged>(this, SelectedFileChanged);
    }

    private void SelectedFileChanged(object recipient, SelectedFileChanged message)
    {
        SelectedImage = null;
        
        if(message.Node.IsFolder) return;
        if(!_fileService.Data!.ShowImageView) return;
        if(message.Content == null || message.Content.Length == 0) return;

        var imageType = ImageHelper.GetImageType(message.Node.Name.ToLower());

        SelectedImage = imageType switch
        {
            ImageType.Standard => ImageHelper.ConvertPngImage(message.Content),
            ImageType.Dds => ImageHelper.ConvertDdsImage(message.Content),
            _ => null
        };
    }
}