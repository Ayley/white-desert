using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using White_Desert.Helper.Files;
using White_Desert.Messages;
using White_Desert.Models;
using White_Desert.Services.Contracts;

namespace White_Desert.ViewModels.Editor;

public partial class HexEditorViewModel : ViewModelBase
{
    [ObservableProperty] private IBufferSource? _hexDataSource;

    private readonly IFileService<AppSettings> _fileService;

    
    public HexEditorViewModel(IFileService<AppSettings> fileService)
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
        HexDataSource?.Dispose();
        HexDataSource = null;
        
        if(message.Node.IsFolder) return;
        if(!_fileService.Data!.ShowHexView) return;
        
        try
        {
            HexDataSource = new FileBufferSource(message.TempFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning("Cannot load HexView: {Msg}", ex.Message);
        }
    }
}